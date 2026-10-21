using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniPdf {
    internal sealed class SignatureReport {
        internal string Name,Details,SubFilter;
        internal bool Integrity,WholeFile,Trusted;
        internal X509Certificate2 Certificate;
    }
    internal static class PdfSignatures {
        const int Reserve=16384,MaxBytes=128*1024*1024;
        static readonly Encoding Bytes=Encoding.GetEncoding(28591);
        internal static void CheckCertificate(X509Certificate2 cert){PdfDocument.Check(cert!=null&&cert.HasPrivateKey,UiText.Get("证书没有可用的私钥。"));PdfDocument.Check(DateTime.Now>=cert.NotBefore&&DateTime.Now<=cert.NotAfter,UiText.Get("证书已过期或尚未生效。"));foreach(var extension in cert.Extensions){var usage=extension as X509KeyUsageExtension;if(usage!=null)PdfDocument.Check((usage.KeyUsages&(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.NonRepudiation))!=0,UiText.Get("该证书不允许数字签名。"));}}
        internal static byte[] Sign(PdfDocument document,X509Certificate2 cert,string reason,string location) {
            CheckCertificate(cert);PdfDocument.Check(document.CanEdit&&document.SignatureCount==0,UiText.Get("已有签名的文档以只读方式打开，暂不支持追加签名。"));
            PdfDocument.Check(N.FPDF_GetSecurityHandlerRevision(document.Handle)<0,UiText.Get("请先将加密 PDF 提取为未加密副本，再进行证书签名。"));
            byte[] data;using(var copy=PdfDocument.FromBytes(document.Serialize(false),""))data=copy.Serialize(true);
            PdfDocument.Check(data.Length<MaxBytes,UiText.Get("证书签名目前支持 128 MiB 以内的 PDF。"));
            var pdf=new PdfSyntax(data);var catalog=pdf.ObjectDictionary(pdf.Root);var form=new Dictionary<string,string>();string original;
            if(catalog.TryGetValue("AcroForm",out original)&&original!="null")form=pdf.DictionaryValue(original);
            PdfDocument.Check(!form.ContainsKey("XFA"),UiText.Get("XFA 表单暂不支持证书签名。"));
            string fields="[]";if(form.TryGetValue("Fields",out original))fields=pdf.Resolve(original).Trim();PdfDocument.Check(fields.StartsWith("[")&&fields.EndsWith("]"),UiText.Get("PDF 表单结构不支持签名。"));
            int signature=pdf.Size,field=signature+1,acro=signature+2;
            form["Fields"]=fields.Substring(0,fields.Length-1)+" "+field+" 0 R]";form["SigFlags"]="3";catalog["AcroForm"]=acro+" 0 R";
            var additions=new SortedDictionary<int,string>();additions[pdf.Root]=PdfSyntax.Dictionary(catalog);additions[field]="<< /FT /Sig /T (MiniPDF_"+Guid.NewGuid().ToString("N")+") /V "+signature+" 0 R >>";additions[acro]=PdfSyntax.Dictionary(form);
            string ranges="[0000000000 0000000000 0000000000 0000000000]";
            additions[signature]="<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange "+ranges+" /Contents <"+new string('0',Reserve*2)+"> /M (D:"+DateTime.UtcNow.ToString("yyyyMMddHHmmss",CultureInfo.InvariantCulture)+"Z) /Name "+Unicode(cert.GetNameInfo(X509NameType.SimpleName,false))+" /Reason "+Unicode(reason)+" /Location "+Unicode(location)+" >>";
            byte[] output;int gap=0,end=0,rangeAt=0;
            using(var stream=new MemoryStream()){stream.Write(data,0,data.Length);Write(stream,"\n");var offsets=new SortedDictionary<int,long>();
                foreach(var entry in additions){offsets[entry.Key]=stream.Position;Write(stream,entry.Key+" "+(entry.Key==pdf.Root?pdf.RootGeneration:0)+" obj\n");if(entry.Key==signature){long start=stream.Position;rangeAt=(int)start+entry.Value.IndexOf(ranges,StringComparison.Ordinal);gap=(int)start+entry.Value.IndexOf("/Contents <",StringComparison.Ordinal)+10;end=gap+Reserve*2+2;}Write(stream,entry.Value+"\nendobj\n");}
                long xref=stream.Position;Write(stream,"xref\n");foreach(var entry in offsets)Write(stream,entry.Key+" 1\n"+entry.Value.ToString("0000000000",CultureInfo.InvariantCulture)+" "+(entry.Key==pdf.Root?pdf.RootGeneration:0).ToString("00000",CultureInfo.InvariantCulture)+" n \n");
                var trailer=new Dictionary<string,string>(pdf.Trailer);trailer["Size"]=(acro+1).ToString(CultureInfo.InvariantCulture);trailer["Prev"]=pdf.StartXref.ToString(CultureInfo.InvariantCulture);trailer.Remove("XRefStm");Write(stream,"trailer\n"+PdfSyntax.Dictionary(trailer)+"\nstartxref\n"+xref+"\n%%EOF\n");output=stream.ToArray();}
            string value="[0000000000 "+gap.ToString("0000000000",CultureInfo.InvariantCulture)+" "+end.ToString("0000000000",CultureInfo.InvariantCulture)+" "+(output.Length-end).ToString("0000000000",CultureInfo.InvariantCulture)+"]";Array.Copy(Bytes.GetBytes(value),0,output,rangeAt,value.Length);
            var cms=new SignedCms(new ContentInfo(Join(output,gap,end,output.Length-end)),true);var signer=new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber,cert){DigestAlgorithm=new Oid("2.16.840.1.101.3.4.2.1"),IncludeOption=X509IncludeOption.EndCertOnly};signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow));cms.ComputeSignature(signer);byte[] signatureBytes=cms.Encode();PdfDocument.Check(signatureBytes.Length<=Reserve,UiText.Get("证书签名数据过大。"));
            string hex=BitConverter.ToString(signatureBytes).Replace("-","");Array.Copy(Bytes.GetBytes(hex),0,output,gap+1,hex.Length);
            using(var verify=PdfDocument.FromBytes(output,"")){PdfDocument.Check(verify.Count==document.Count&&verify.SignatureCount==1,UiText.Get("签名文件校验失败。"));var reports=Inspect(verify);try{PdfDocument.Check(reports.Count==1&&reports[0].Integrity&&reports[0].WholeFile,UiText.Get("签名文件校验失败。"));}finally{DisposeReports(reports);}}return output;
        }
        static void Write(Stream stream,string text){byte[] b=Bytes.GetBytes(text);stream.Write(b,0,b.Length);}
        static string Unicode(string value){return "<FEFF"+BitConverter.ToString(Encoding.BigEndianUnicode.GetBytes(value??"")).Replace("-","")+">";}
        static byte[] Join(byte[] bytes,int firstLength,int secondOffset,int secondLength){var joined=new byte[checked(firstLength+secondLength)];Array.Copy(bytes,0,joined,0,firstLength);Array.Copy(bytes,secondOffset,joined,firstLength,secondLength);return joined;}
        internal static void DisposeReports(List<SignatureReport> reports){foreach(var r in reports)if(r.Certificate!=null)r.Certificate.Dispose();}
        internal static List<SignatureReport> Inspect(PdfDocument doc){var results=new List<SignatureReport>();byte[] data=doc.OriginalBytes;for(int i=0;i<Math.Min(100,doc.SignatureCount);i++){var report=new SignatureReport{Name=UiText.Get("签名")+" "+(i+1),Details=""};results.Add(report);try{
                    PdfDocument.Check(data!=null&&data.Length<=MaxBytes,UiText.Get("签名验证目前支持 128 MiB 以内的 PDF。"));IntPtr handle=N.FPDF_GetSignatureObject(doc.Handle,i);uint length=N.FPDFSignatureObj_GetContents(handle,null,0);PdfDocument.Check(length>0&&length<=1024*1024,UiText.Get("签名数据无效。"));var contents=new byte[length];N.FPDFSignatureObj_GetContents(handle,contents,length);
                    uint filterLength=N.FPDFSignatureObj_GetSubFilter(handle,null,0);PdfDocument.Check(filterLength>0&&filterLength<256,UiText.Get("签名格式无效。"));var filter=new byte[filterLength];N.FPDFSignatureObj_GetSubFilter(handle,filter,filterLength);report.SubFilter=Encoding.ASCII.GetString(filter).TrimEnd('\0');
                    PdfDocument.Check(report.SubFilter=="adbe.pkcs7.detached"||report.SubFilter=="ETSI.CAdES.detached",UiText.Get("暂不支持验证这种签名格式。"));
                    PdfDocument.Check(N.FPDFSignatureObj_GetByteRange(handle,null,0)==4,UiText.Get("签名字节范围无效。"));var range=new int[4];N.FPDFSignatureObj_GetByteRange(handle,range,4);PdfDocument.Check(range[0]==0&&range[1]>0&&range[2]>range[1]&&range[3]>=0&&(long)range[2]+range[3]<=data.Length,UiText.Get("签名字节范围无效。"));
                    // Only the exact hex Contents string may be excluded from the digest.
                    PdfDocument.Check(data[range[1]]=='<'&&data[range[2]-1]=='>',UiText.Get("签名排除范围不安全。"));var excluded=new StringBuilder();for(int p=range[1]+1;p<range[2]-1;p++)if(!Char.IsWhiteSpace((char)data[p])){char c=(char)data[p];PdfDocument.Check(Uri.IsHexDigit(c),UiText.Get("签名排除范围不安全。"));excluded.Append(c);}
                    PdfDocument.Check(excluded.Length==contents.Length*2,UiText.Get("签名排除范围不安全。"));for(int p=0;p<contents.Length;p++)PdfDocument.Check(contents[p]==Convert.ToByte(excluded.ToString(p*2,2),16),UiText.Get("签名排除范围不安全。"));
                    int derLength=DerLength(contents);var der=new byte[derLength];Array.Copy(contents,der,derLength);for(int p=derLength;p<contents.Length;p++)PdfDocument.Check(contents[p]==0,UiText.Get("签名填充数据无效。"));
                    var cms=new SignedCms(new ContentInfo(Join(data,range[1],range[2],range[3])),true);cms.Decode(der);cms.CheckSignature(true);PdfDocument.Check(cms.SignerInfos.Count==1&&cms.SignerInfos[0].Certificate!=null,UiText.Get("签名证书缺失。"));report.Integrity=true;report.WholeFile=(long)range[2]+range[3]==data.Length;report.Certificate=new X509Certificate2(cms.SignerInfos[0].Certificate);report.Name=report.Certificate.GetNameInfo(X509NameType.SimpleName,false);
                    report.Trusted=WindowsCertificates.TrustedOffline(report.Certificate);
                    report.Details=(report.WholeFile?UiText.Get("签名字节校验通过，覆盖当前文件。"):UiText.Get("原签署版本的字节校验通过；文件含后续修改，未验证这些修改。"))+Environment.NewLine+(report.Trusted?UiText.Get("Windows 本地证书链受信任。"):UiText.Get("Windows 本地证书链不受信任或不完整。"))+Environment.NewLine+UiText.Get("离线校验：未检查证书吊销、可信时间戳及文档修改权限。")+Environment.NewLine+"CMS digest OID: "+cms.SignerInfos[0].DigestAlgorithm.Value;
                }catch(Exception e){report.Details=UiText.Get("未通过验证：")+e.Message;}}
            return results;
        }
        static int DerLength(byte[] der){PdfDocument.Check(der.Length>2&&der[0]==0x30,UiText.Get("签名数据无效。"));int count=der[1],header=2;if((count&128)!=0){int n=count&127;PdfDocument.Check(n>0&&n<=4&&der.Length>=2+n,UiText.Get("签名数据无效。"));count=0;for(int i=0;i<n;i++)count=checked(count*256+der[2+i]);header+=n;}PdfDocument.Check(count>0&&(long)header+count<=der.Length,UiText.Get("签名数据无效。"));return header+count;}
        // This parser accepts only PDFium's fresh, unencrypted classic-xref output.
        // Offsets come from xref entries; PDF stream contents are never searched for objects.
        sealed class PdfSyntax {
            readonly string text;
            readonly Dictionary<int,int> offsets=new Dictionary<int,int>();
            internal readonly Dictionary<string,string> Trailer;
            internal readonly int Root,RootGeneration,Size,StartXref;
            internal PdfSyntax(byte[] data){text=Bytes.GetString(data);int marker=text.LastIndexOf("startxref",StringComparison.Ordinal);Check(marker>=0);int at=marker+9;StartXref=Int32.Parse(Atom(ref at),CultureInfo.InvariantCulture);at=StartXref;Check(Atom(ref at)=="xref");
                while(true){string first=Atom(ref at);if(first=="trailer")break;int start=Int32.Parse(first,CultureInfo.InvariantCulture),count=Int32.Parse(Atom(ref at),CultureInfo.InvariantCulture);Check(start>=0&&count>=0&&count<=1000000);for(int i=0;i<count;i++){int offset=Int32.Parse(Atom(ref at),CultureInfo.InvariantCulture);Atom(ref at);if(Atom(ref at)=="n")offsets[start+i]=offset;}}
                Trailer=ParseDictionary(Read(ref at));Check(!Trailer.ContainsKey("Encrypt"));Size=Int32.Parse(Trailer["Size"],CultureInfo.InvariantCulture);int[] root=Reference(Trailer["Root"]);Root=root[0];RootGeneration=root[1];Check(Size>0&&Size<10000000);}
            static void Check(bool ok){PdfDocument.Check(ok,UiText.Get("PDF 结构不支持安全签名，请重新导出 PDF 后再试。"));}
            void Space(ref int p){while(p<text.Length){if(Char.IsWhiteSpace(text[p])||text[p]=='\0'){p++;continue;}if(text[p]=='%'){while(p<text.Length&&text[p]!='\r'&&text[p]!='\n')p++;continue;}break;}}
            static bool Delimiter(char c){return Char.IsWhiteSpace(c)||"/[]<>()%\0".IndexOf(c)>=0;}
            string Atom(ref int p){Space(ref p);int start=p;if(p<text.Length&&text[p]=='/')p++;while(p<text.Length&&!Delimiter(text[p]))p++;Check(p>start);return text.Substring(start,p-start);}
            string Read(ref int p){Space(ref p);int start=p;Check(p<text.Length);char c=text[p++];if(c=='<'){if(p<text.Length&&text[p]=='<'){p++;while(true){Space(ref p);Check(p+1<text.Length);if(text[p]=='>'&&text[p+1]=='>'){p+=2;break;}Read(ref p);}}else{while(p<text.Length&&text[p]!='>')p++;Check(p<text.Length);p++;}}
                else if(c=='['){while(true){Space(ref p);Check(p<text.Length);if(text[p]==']'){p++;break;}Read(ref p);}}
                else if(c=='('){int depth=1;while(depth>0&&p<text.Length){char ch=text[p++];if(ch=='\\'){if(p<text.Length)p++;}else if(ch=='(')depth++;else if(ch==')')depth--;}Check(depth==0);}
                else{p=start;Atom(ref p);int firstEnd=p;if(Char.IsDigit(c)){try{string second=Atom(ref p);int n;if(!Int32.TryParse(second,out n)||Atom(ref p)!="R")p=firstEnd;}catch{p=firstEnd;}}}return text.Substring(start,p-start);}
            Dictionary<string,string> ParseDictionary(string value){var parser=new PdfSyntax(value);int p=2;var dict=new Dictionary<string,string>();Check(value.StartsWith("<<"));while(true){parser.Space(ref p);Check(p+1<value.Length);if(value[p]=='>'&&value[p+1]=='>')break;string name=parser.Atom(ref p);Check(name.StartsWith("/"));name=Regex.Replace(name.Substring(1),"#([0-9A-Fa-f]{2})",delegate(Match m){return ((char)Convert.ToByte(m.Groups[1].Value,16)).ToString();});Check(!dict.ContainsKey(name));dict[name]=parser.Read(ref p);}return dict;}
            PdfSyntax(string fragment){text=fragment;}
            static int[] Reference(string value){var match=Regex.Match(value,@"^\s*(\d+)\s+(\d+)\s+R\s*$");Check(match.Success);return new[]{Int32.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),Int32.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture)};}
            internal string Resolve(string value){if(value.TrimStart().StartsWith("<<")||value.TrimStart().StartsWith("["))return value;int[] reference=Reference(value);return Object(reference[0],reference[1]);}
            string Object(int id,int generation){int at;Check(offsets.TryGetValue(id,out at));Check(Atom(ref at)==id.ToString(CultureInfo.InvariantCulture)&&Atom(ref at)==generation.ToString(CultureInfo.InvariantCulture)&&Atom(ref at)=="obj");return Read(ref at);}
            internal Dictionary<string,string> ObjectDictionary(int id){return ParseDictionary(Object(id,RootGeneration));}
            internal Dictionary<string,string> DictionaryValue(string value){return ParseDictionary(Resolve(value));}
            internal static string Dictionary(Dictionary<string,string> dictionary){var b=new StringBuilder("<<");foreach(var entry in dictionary){string key=Regex.Replace(entry.Key,@"[^a-zA-Z0-9_]",delegate(Match m){return "#"+((int)m.Value[0]).ToString("X2",CultureInfo.InvariantCulture);});b.Append(" /").Append(key).Append(' ').Append(entry.Value);}return b.Append(" >>").ToString();}
        }
    }
    internal sealed partial class PdfDocument {
        internal int SignatureCount {get{return Handle==IntPtr.Zero?0:Math.Max(0,N.FPDF_GetSignatureCount(Handle));}}
        internal byte[] OriginalBytes {get{return inputSource!=null?inputSource.ReadAll():null;}}
    }
}
