using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MiniPdf
{
    // Restore document-level optional-content defaults after PDFium page import.
    // Work only on private, freshly serialized copies; content streams stay native.
    internal sealed class LayerImport : IDisposable
    {
        const string Tag = "MiniPDFImportLayer";
        sealed class Source
        {
            internal PdfDocument Prepared;
            internal PdfSyntax Syntax;
            internal Dictionary<string,string> Default, View;
            internal readonly Dictionary<int,string> Groups = new Dictionary<int,string>();
            internal readonly Dictionary<int,List<string>> Imported = new Dictionary<int,List<string>>();
        }
        readonly List<Source> sources = new List<Source>();
        readonly Dictionary<PdfDocument,Source> byDocument = new Dictionary<PdfDocument,Source>();
        readonly Dictionary<PdfDocument,PdfDocument> prepared = new Dictionary<PdfDocument,PdfDocument>();
        readonly Dictionary<string,Source> byTag = new Dictionary<string,Source>();
        bool separateView;

        static void Check(bool condition)
        {
            if (!condition) throw new IOException("Cannot preserve this PDF's optional-content configuration. Merge was canceled.");
        }
        static bool HasKey(byte[] data, string value)
        {
            byte[] key = Encoding.ASCII.GetBytes(value);
            for (int i=0;i<=data.Length-key.Length;i++)
            {
                if (data[i] != key[0]) continue;
                int at=1; while(at<key.Length && data[i+at]==key[at]) at++;
                if(at==key.Length) return true;
            }
            return false;
        }
        static bool HasContentsArray(byte[] data)
        {
            byte[] key=Encoding.ASCII.GetBytes("/Contents");
            for(int i=0;i<=data.Length-key.Length;i++)
            {
                if(data[i]!=key[0]) continue;
                int at=1; while(at<key.Length && data[i+at]==key[at]) at++;
                if(at!=key.Length) continue;
                at+=i; while(at<data.Length && data[at]<=32) at++;
                if(at<data.Length && data[at]=='[') return true;
            }
            return false;
        }
        static string Get(Dictionary<string,string> values, string key, string fallback)
        {
            string value;
            return values.TryGetValue(key,out value) ? value : fallback;
        }
        static bool Is(string value, string name)
        {
            return value == "/"+name || value == "("+name+")";
        }
        static Dictionary<string,string> Config(PdfSyntax syntax, Dictionary<string,string> properties)
        {
            string value;
            return properties.TryGetValue("D",out value) && value != "null" ? syntax.DictionaryValue(value) : new Dictionary<string,string>();
        }
        static bool ViewIntent(PdfSyntax syntax, Dictionary<string,string> config)
        {
            string value;
            if (!config.TryGetValue("Intent",out value)) return false;
            int id;
            if (PdfSyntax.TryReference(value,out id)) value=syntax.Resolve(value);
            if (value.TrimStart().StartsWith("["))
            {
                foreach(var item in syntax.ArrayValues(value)) if(Is(item,"View") || Is(item,"All")) return true;
                return false;
            }
            return Is(value,"View") || Is(value,"All");
        }
        bool importedForms;
        internal LayerImport(IList<PdfDocument> documents)
        {
            try { foreach(var document in documents) Prepare(document); }
            catch { Dispose(); throw; }
        }
        void Prepare(PdfDocument document)
        {
            byte[] bytes=document.Serialize(false,true);
            bool forms; bytes=FormImport.Tag(document,bytes,out forms); importedForms|=forms;
            if(forms)using(var normalize=PdfDocument.FromBytes(bytes,""))bytes=normalize.Serialize(false);
            bool possibleLayers=HasKey(bytes,"/OCProperties");
            bool repair=HasContentsArray(bytes) || N.FPDF_DocumentHasValidCrossReferenceTable(document.Handle)==0;
            if(!possibleLayers && !repair) { if(forms)prepared[document]=PdfDocument.FromBytes(bytes,""); return; }
            var syntax=new PdfSyntax(bytes);
            var changes=new SortedDictionary<int,string>();
            if(repair)
            {
                // An invalid entry must not make PDFium discard every valid content stream.
                foreach(int id in syntax.ObjectNumbers)
                {
                    string value=syntax.ObjectValue(id);
                    if(!value.StartsWith("<<")) continue;
                    var page=syntax.ParseValueDictionary(value);
                    if(!Is(Get(page,"Type",""),"Page") || !page.TryGetValue("Contents",out value)) continue;
                    int reference;
                    if(PdfSyntax.TryReference(value,out reference) && !syntax.ContainsObject(reference)) continue;
                    string contents=PdfSyntax.TryReference(value,out reference) ? syntax.ObjectValue(reference) : value;
                    if(!contents.TrimStart().StartsWith("[")) continue;
                    var valid=new List<string>(); var all=syntax.ArrayValues(contents);
                    foreach(var item in all) if(syntax.IsStreamReference(item)) valid.Add(item);
                    if(valid.Count==all.Count) continue;
                    page["Contents"]="["+String.Join(" ",valid)+"]";
                    changes[id]=PdfSyntax.Dictionary(page);
                }
            }
            Source source=null;
            if(possibleLayers)
            {
                var catalog=syntax.ObjectDictionary(syntax.Root);
                string value;
                if(catalog.TryGetValue("OCProperties",out value) && value!="null")
                {
                    var properties=syntax.DictionaryValue(value);
                    if(properties.TryGetValue("OCGs",out value) && value!="null")
                    {
                        var groups=syntax.ArrayValues(value); Check(groups.Count<=10000);
                        if(groups.Count>0)
                        {
                            source=new Source { Syntax=syntax,Default=Config(syntax,properties) };
                            source.View=source.Default; sources.Add(source); byDocument.Add(document,source);
                            if(properties.TryGetValue("Configs",out value) && value!="null")
                                foreach(var raw in syntax.ArrayValues(value))
                                {
                                    var config=syntax.DictionaryValue(raw);
                                    if(!ViewIntent(syntax,config)) continue;
                                    source.View=config; separateView=true; break;
                                }
                            string prefix=Guid.NewGuid().ToString("N");
                            foreach(var group in groups)
                            {
                                int id; Check(PdfSyntax.TryReference(group,out id));
                                if(source.Groups.ContainsKey(id)) continue;
                                var dictionary=syntax.ObjectDictionary(id);
                                Check(Is(Get(dictionary,"Type","/OCG"),"OCG"));
                                string tag="("+prefix+"_"+id+")";
                                dictionary[Tag]=tag; source.Groups.Add(id,tag); byTag.Add(tag,source);
                                changes[id]=PdfSyntax.Dictionary(dictionary);
                            }
                        }
                    }
                }
            }
            if(changes.Count==0) {if(forms)prepared[document]=PdfDocument.FromBytes(bytes,"");return;}
            var copy=PdfDocument.FromBytes(syntax.Append(bytes,changes),"");
            prepared.Add(document,copy);
            if(source!=null) source.Prepared=copy;
        }
        internal PdfDocument Prepared(PdfDocument original)
        {
            PdfDocument copy;
            return prepared.TryGetValue(original,out copy) ? copy : original;
        }
        static HashSet<int> GroupSet(Source source, Dictionary<string,string> config, string name)
        {
            var result=new HashSet<int>(); string value;
            if(config.TryGetValue(name,out value)) foreach(var group in source.Syntax.ArrayValues(value))
            { int id; if(PdfSyntax.TryReference(group,out id)) result.Add(id); }
            return result;
        }
        string Clone(Source source, string value, HashSet<int> active, int depth)
        {
            Check(depth<48);
            int id;
            if(PdfSyntax.TryReference(value,out id))
            {
                if(source.Groups.ContainsKey(id))
                {
                    List<string> imported;
                    return source.Imported.TryGetValue(id,out imported) ? imported[0] : null;
                }
                Check(active.Add(id));
                string cloned=Clone(source,source.Syntax.Resolve(value),active,depth+1);
                active.Remove(id);
                return cloned;
            }
            value=value.Trim();
            if(value.StartsWith("["))
            {
                var items=new List<string>();
                foreach(var item in source.Syntax.ArrayValues(value))
                {
                    if(PdfSyntax.TryReference(item,out id) && source.Groups.ContainsKey(id))
                    {
                        List<string> imported;
                        if(source.Imported.TryGetValue(id,out imported)) items.AddRange(imported);
                    }
                    else { string cloned=Clone(source,item,active,depth+1); if(cloned!=null) items.Add(cloned); }
                }
                return "["+String.Join(" ",items)+"]";
            }
            if(value.StartsWith("<<"))
            {
                var result=new Dictionary<string,string>();
                foreach(var item in source.Syntax.ParseValueDictionary(value))
                {
                    string cloned=Clone(source,item.Value,active,depth+1);
                    if(cloned!=null) result[item.Key]=cloned;
                }
                return PdfSyntax.Dictionary(result);
            }
            return value;
        }
        Dictionary<string,string> Combined(bool view)
        {
            var on=new List<string>(); var off=new List<string>(); var automatic=new List<string>();
            var order=new List<string>(); var locked=new List<string>(); var radio=new List<string>();
            foreach(var source in sources)
            {
                var config=view?source.View:source.Default;
                bool defaultOn=!Is(Get(config,"BaseState","/ON"),"OFF");
                var enabled=GroupSet(source,config,"ON"); var disabled=GroupSet(source,config,"OFF");
                foreach(var group in source.Imported)
                {
                    bool state=enabled.Contains(group.Key) || defaultOn;
                    if(disabled.Contains(group.Key)) state=false;
                    (state?on:off).AddRange(group.Value);
                }
                foreach(var key in new[]{"AS","Order","Locked","RBGroups"})
                {
                    string value;
                    if(!config.TryGetValue(key,out value)) continue;
                    string mapped=Clone(source,value,new HashSet<int>(),0);
                    var items=source.Syntax.ArrayValues(mapped);
                    if(key=="AS") automatic.AddRange(items);
                    else if(key=="Order") order.AddRange(items);
                    else if(key=="Locked") locked.AddRange(items);
                    else radio.AddRange(items);
                }
            }
            var result=new Dictionary<string,string>{{"Name","(Mini PDF imported defaults)"},{"BaseState","/ON"},{"Intent","/View"},
                {"ON","["+String.Join(" ",on)+"]"},{"OFF","["+String.Join(" ",off)+"]"}};
            if(automatic.Count>0) result["AS"]="["+String.Join(" ",automatic)+"]";
            if(order.Count>0) result["Order"]="["+String.Join(" ",order)+"]";
            if(locked.Count>0) result["Locked"]="["+String.Join(" ",locked)+"]";
            if(radio.Count>0) result["RBGroups"]="["+String.Join(" ",radio)+"]";
            return result;
        }
        byte[] RestoreLayers(byte[] bytes)
        {
            if(sources.Count==0) return bytes;
            var syntax=new PdfSyntax(bytes);
            var changes=new SortedDictionary<int,string>();
            var groups=new List<string>();
            foreach(int id in syntax.ObjectNumbers)
            {
                string value=syntax.ObjectValue(id);
                if(!value.StartsWith("<<")) continue;
                var dictionary=syntax.ParseValueDictionary(value);
                string tag; Source source;
                if(!dictionary.TryGetValue(Tag,out tag) || !byTag.TryGetValue(tag,out source)) continue;
                Check(Is(Get(dictionary,"Type","/OCG"),"OCG"));
                int oldId=-1;
                foreach(var item in source.Groups) if(item.Value==tag) { oldId=item.Key; break; }
                Check(oldId>=0);
                List<string> mapped;
                if(!source.Imported.TryGetValue(oldId,out mapped)) source.Imported.Add(oldId,mapped=new List<string>());
                string reference=syntax.ObjectReference(id); mapped.Add(reference); groups.Add(reference);
                dictionary.Remove(Tag); changes[id]=PdfSyntax.Dictionary(dictionary);
            }
            if(groups.Count==0) return bytes;
            var properties=new Dictionary<string,string>{{"OCGs","["+String.Join(" ",groups)+"]"},
                {"D",PdfSyntax.Dictionary(Combined(false))}};
            if(separateView) properties["Configs"]="["+PdfSyntax.Dictionary(Combined(true))+"]";
            var catalog=syntax.ObjectDictionary(syntax.Root);
            catalog["OCProperties"]=PdfSyntax.Dictionary(properties);
            changes[syntax.Root]=PdfSyntax.Dictionary(catalog);
            return syntax.Append(bytes,changes);
        }
        internal byte[] Restore(byte[] bytes) {
            byte[] result=RestoreLayers(bytes);
            if(!importedForms)return result;
            if(sources.Count>0)using(var normalized=PdfDocument.FromBytes(result,""))result=normalized.Serialize(false);
            return FormImport.Recover(result);
        }
        internal void BeginTarget(PdfDocument target)
        {
            if(sources.Count==0 && !importedForms) return;
            if(N.FPDF_GetSecurityHandlerRevision(target.Handle)>=0)
                throw new IOException("Layered/form imports into an encrypted document are not supported. Export an unencrypted copy explicitly first.");
            PdfDocument source;
            if(prepared.TryGetValue(target,out source)) target.ReloadForLayerImport(source.Serialize(false));
        }
        internal void EndTarget(PdfDocument target)
        {
            if(sources.Count>0 || importedForms) {byte[] data=Restore(target.Serialize(false));target.ReloadForLayerImport(data);}
        }
        public void Dispose()
        {
            foreach(var copy in prepared.Values) copy.Dispose();
            prepared.Clear(); sources.Clear(); byDocument.Clear(); byTag.Clear();
        }
    }
}
