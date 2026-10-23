using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniPdf {
    internal static partial class N {
        [DllImport(D)] internal static extern int FPDFText_IsGenerated(IntPtr text,int index);
    }
    internal sealed class SelectedTextPart {
        internal int Index,From,Count;
        internal float[] Positions;
    }
    internal sealed class SelectedTextRow {
        internal float X,Y;
    }
    internal sealed class SelectedTextData {
        internal int[] Styles;
        internal float[] Advances;
        internal List<SelectedTextPart> Parts=new List<SelectedTextPart>();
        internal List<SelectedTextRow> Rows=new List<SelectedTextRow>();
    }
    internal sealed partial class PdfDocument {
        const string PageDivider="\n\n──── Page {0} ────\n\n";
        internal static string SelectionDivider(int page){return String.Format(System.Globalization.CultureInfo.InvariantCulture,PageDivider,page+1);}
        internal TextEditSelection SelectTextEdit(List<TextRange> ranges) {
            Check(ranges!=null&&ranges.Count>0,UiText.Get("请先选择文字。"));
            int saved=Index;var children=new List<TextEditSelection>();
            try{foreach(var range in ranges){OpenPage(range.Page);children.Add(SelectTextRange(range));}}
            finally{OpenPage(saved);}
            var first=children[0];if(children.Count==1)return first;
            var content=new StringBuilder();
            foreach(var child in children){content.Append(SelectionDivider(child.Page));content.Append(child.Text);}
            Check(content.Length<=20000,UiText.Get("文字块较大，请缩小编辑范围。"));
            return new TextEditSelection{Page=first.Page,Epoch=RenderEpoch,Anchor=first.Anchor,Scope=3,
                Indices=children.SelectMany(c=>c.Indices).ToArray(),Originals=children.SelectMany(c=>c.Originals).ToArray(),
                Text=content.ToString(),FontName=first.FontName,Size=first.Size,SizeScale=first.SizeScale,
                Scale=first.Scale,Width=first.Width,LineHeight=first.LineHeight,Color=first.Color,Bounds=first.Bounds,
                Restructure=true,ChangeFont=true,Children=children.ToArray()};
        }
        TextEditSelection SelectTextRange(TextRange range) {
            int total=N.FPDFText_CountChars(TextPage);
            Check(range.Start>=0&&range.Count>0&&range.Start<=total-range.Count&&total<=200000,
                UiText.Get("文字已变化，请重新选择。"));
            var selectedObjects=new HashSet<IntPtr>();
            for(int i=range.Start;i<range.Start+range.Count;i++)selectedObjects.Add(N.FPDFText_GetTextObject(TextPage,i));
            var runs=new Dictionary<IntPtr,TextRun>();
            for(int i=0;i<N.FPDFPage_CountObjects(Page);i++){
                IntPtr obj=N.FPDFPage_GetObject(Page,i);
                if(selectedObjects.Contains(obj)&&N.FPDFPageObj_GetType(obj)==1)runs[obj]=ReadTextRun(i);
            }
            var offsets=new Dictionary<IntPtr,int>();var source=new Dictionary<IntPtr,StringBuilder>();
            var origins=new Dictionary<IntPtr,List<float>>();var data=new SelectedTextData();
            var partIndex=new Dictionary<IntPtr,int>();var styles=new List<int>();var glyphX=new List<float>();var glyphY=new List<float>();var text=new StringBuilder();
            N.Rect bounds=new N.Rect();bool any=false;
            for(int i=0;i<total;i++) {
                IntPtr obj=N.FPDFText_GetTextObject(TextPage,i);
                bool real=N.FPDFText_IsGenerated(TextPage,i)==0;
                string value=SelectedText(i,1).Replace("\r","");
                int offset=0;TextRun run=null;int style=-1;float ox=Single.NaN,oy=Single.NaN;
                if(obj!=IntPtr.Zero&&runs.TryGetValue(obj,out run)&&run!=null) {
                    if(!offsets.TryGetValue(obj,out offset)){source[obj]=new StringBuilder();origins[obj]=new List<float>();}
                    source[obj].Append(value);offsets[obj]=offset+value.Length;
                    double x,y;float px=0,py=0;
                    Check(N.FPDFText_GetCharOrigin(TextPage,i,out x,out y)!=0&&Project(run.Matrix,(float)x,(float)y,out px,out py),
                        UiText.Get("无法读取文字位置。"));
                    ox=(float)x;oy=(float)y;for(int j=0;j<value.Length;j++)origins[obj].Add(px);
                }
                if(i<range.Start||i>=range.Start+range.Count||value.Length==0)continue;
                if(!String.IsNullOrWhiteSpace(value))Check(run!=null&&real,UiText.Get("选区包含嵌套或无法映射的文字，暂不支持安全改写。"));
                if(run!=null) {
                    Check(run.Safe&&Math.Abs(run.Matrix.b)<.001f&&Math.Abs(run.Matrix.c)<.001f&&run.Matrix.a>0&&run.Matrix.d>0,
                        UiText.Get("选区包含隐藏、剪裁、标记或非横排文字，请按单个对象编辑。"));
                    if(!partIndex.TryGetValue(obj,out style)){style=data.Parts.Count;partIndex.Add(obj,style);data.Parts.Add(new SelectedTextPart{Index=run.Index,From=offset});}
                    var part=data.Parts[style];Check(offset==part.From+part.Count,UiText.Get("文字映射不连续，请缩小选区。"));part.Count+=value.Length;
                    double x,y;N.FPDFText_GetCharOrigin(TextPage,i,out x,out y);
                    if(data.Rows.Count==0||text.Length>0&&text[text.Length-1]=='\n')data.Rows.Add(new SelectedTextRow{X=(float)x,Y=(float)y});
                    N.Rect box;if(N.FPDFText_GetLooseCharBox(TextPage,i,out box)!=0){bounds=any?UnionTextBounds(bounds,box):box;any=true;}
                }
                text.Append(value);for(int j=0;j<value.Length;j++){styles.Add(style);glyphX.Add(ox);glyphY.Add(oy);}
            }
            Check(any&&data.Parts.Count>0&&data.Parts.Count<=512&&text.Length<=20000,UiText.Get("文字块较大，请缩小编辑范围。"));
            var indices=new List<int>();var originals=new List<string>();
            foreach(var pair in partIndex) {
                var run=runs[pair.Key];var part=data.Parts[pair.Value];
                string material=source[pair.Key].ToString();
                Check(run.Text.StartsWith(material,StringComparison.Ordinal)&&String.IsNullOrWhiteSpace(run.Text.Substring(material.Length))&&origins[pair.Key].Count==material.Length,
                    UiText.Get("选区文字编码无法精确映射，请保留原文。"));
                part.Positions=origins[pair.Key].ToArray();indices.Add(run.Index);originals.Add(run.Text);
            }
            data.Styles=styles.ToArray();data.Advances=Enumerable.Repeat(Single.NaN,styles.Count).ToArray();
            for(int i=0;i<styles.Count-1;i++)if(styles[i]>=0&&styles[i+1]>=0&&Math.Abs(glyphY[i]-glyphY[i+1])<.01f&&glyphX[i+1]>=glyphX[i])data.Advances[i]=glyphX[i+1]-glyphX[i];
            Check(text.ToString()==SelectedText(range.Start,range.Count).Replace("\r",""),
                UiText.Get("选区文字编码无法精确映射，请保留原文。"));
            var firstRun=ReadTextRun(data.Parts[0].Index);
            float lineHeight=data.Rows.Count>1?Math.Abs(data.Rows[0].Y-data.Rows[1].Y)/(firstRun.Size*firstRun.Matrix.d):1.4f;
            return new TextEditSelection{Page=Index,Epoch=RenderEpoch,Anchor=firstRun.Index,Scope=3,
                Indices=indices.ToArray(),Originals=originals.ToArray(),Text=text.ToString(),FontName=firstRun.FontName,
                Size=firstRun.Size,SizeScale=firstRun.Matrix.d,Scale=firstRun.Matrix.a,Width=(bounds.right-bounds.left)/firstRun.Matrix.a,
                LineHeight=Math.Max(.8f,Math.Min(3,lineHeight)),Color=firstRun.Color,Bounds=bounds,Restructure=true,ChangeFont=true,Range=data};
        }
        // Preserve unchanged character styles; edited text inherits its nearest original style.
        static int[] ReplacementStyles(string oldText,string newText,int[] oldStyles,out int[] sourceMap) {
            int[] map=Enumerable.Repeat(-1,newText.Length).ToArray();
            int prefix=0,suffix=0;
            while(prefix<oldText.Length&&prefix<newText.Length&&oldText[prefix]==newText[prefix]){map[prefix]=prefix;prefix++;}
            while(suffix<oldText.Length-prefix&&suffix<newText.Length-prefix&&oldText[oldText.Length-1-suffix]==newText[newText.Length-1-suffix]){
                map[newText.Length-1-suffix]=oldText.Length-1-suffix;suffix++;
            }
            int n=oldText.Length-prefix-suffix,m=newText.Length-prefix-suffix;
            if((long)n*m<=2000000&&n>0&&m>0) {
                var dp=new ushort[(n+1)*(m+1)];
                for(int i=n-1;i>=0;i--)for(int j=m-1;j>=0;j--)dp[i*(m+1)+j]=(ushort)(oldText[prefix+i]==newText[prefix+j]?1+dp[(i+1)*(m+1)+j+1]:Math.Max(dp[(i+1)*(m+1)+j],dp[i*(m+1)+j+1]));
                int a=0,b=0;while(a<n&&b<m){if(oldText[prefix+a]==newText[prefix+b]){map[prefix+b]=prefix+a;a++;b++;}else if(dp[(a+1)*(m+1)+b]>=dp[a*(m+1)+b+1])a++;else b++;}
            }
            var result=new int[newText.Length];int previous=-1;
            for(int i=0;i<result.Length;i++){
                int at=map[i];if(at>=0&&oldStyles[at]>=0)previous=oldStyles[at];
                if(previous<0){int next=Array.FindIndex(map,i,x=>x>=0&&oldStyles[x]>=0);if(next>=0)previous=oldStyles[map[next]];}
                result[i]=Math.Max(0,previous);
            }
            sourceMap=map;return result;
        }
        sealed class SelectedOutput {
            internal TextRun Style;
            internal string Text;
            internal float X,Y,Size;
            internal float[] Positions;
        }
        List<SelectedOutput> LayoutSelectedText(TextEditSelection selection,TextEditOptions options,List<TextRun> runs) {
            var data=selection.Range;var first=runs[0];string text=options.Text.Replace("\r","").Replace("\t","    ");
            int[] sourceMap;var styles=ReplacementStyles(selection.Text,text,data.Styles,out sourceMap);
            float ratio=options.Size/selection.Size;
            bool keep=options.Face=="保留原字体";IntPtr replacement=keep?IntPtr.Zero:FontFor(text,options.Face);
            var tracking=runs.Select((run,i)=>Tracking(run,data.Parts[i].Positions)*ratio).ToArray();
            Func<int,float> advanceAt=delegate(int i){int original=sourceMap[i];var style=runs[styles[i]];
                if(keep&&original>=0&&i+1<sourceMap.Length&&sourceMap[i+1]==original+1&&Finite(data.Advances[original]))return data.Advances[original]*ratio;
                return (GlyphWidth(keep?style.Font:replacement,text[i],keep?style.Size*ratio:options.Size)+(keep?tracking[styles[i]]:0))*(keep?style.Matrix.a:first.Matrix.a);};
            float width=options.Width*selection.Scale;
            var output=new List<SelectedOutput>();int row=0,start=0;float gap=options.Size*selection.SizeScale*options.LineHeight;
            while(start<text.Length) {
                bool preserveRows=Near(options.Size,selection.Size)&&Near(options.LineHeight,selection.LineHeight)&&!options.Wrap;
                var pos=preserveRows&&row<data.Rows.Count?data.Rows[row]:new SelectedTextRow{X=row<data.Rows.Count?data.Rows[row].X:data.Rows[Math.Min(1,data.Rows.Count-1)].X,Y=data.Rows[0].Y-gap*row};
                float x=pos.X;int end=start,space=-1;
                while(end<text.Length&&text[end]!='\n') {
                    float advance=advanceAt(end);
                    if(options.Wrap&&x+advance>pos.X+width+.01f)break;
                    x+=advance;if(Char.IsWhiteSpace(text[end]))space=end;end++;
                }
                if(end<text.Length&&text[end]!='\n'&&space>start)end=space+1;
                if(end==start&&text[start]!='\n')throw new InvalidOperationException(UiText.Get("文本框太窄，无法容纳一个字符。"));
                x=pos.X;
                for(int i=start;i<end;) {
                    int stop=i+1;while(stop<end&&(!keep||styles[stop]==styles[i]))stop++;
                    var style=keep?runs[styles[i]]:new TextRun{Font=replacement,Size=options.Size,Matrix=first.Matrix,Color=first.Color,Fill=first.Fill,Stroke=first.Stroke};
                    float raw=keep?style.Size*ratio:options.Size;
                    var offsets=new float[Math.Max(0,stop-i-1)];float local=0;
                    for(int j=i;j<stop;j++){if(j>i)offsets[j-i-1]=local;local+=advanceAt(j)/style.Matrix.a;}
                    output.Add(new SelectedOutput{Style=style,Text=text.Substring(i,stop-i),X=x,Y=pos.Y,Size=raw,Positions=offsets});
                    x+=local*style.Matrix.a;i=stop;
                }
                start=end;if(start<text.Length&&text[start]=='\n')start++;row++;
                Check(row<=400&&output.Count<=1024,UiText.Get("文字块较大，请缩小编辑范围。"));
            }
            return output;
        }
        void ReplaceSelectedCore(TextEditSelection selection,TextEditOptions options,int action=0,float scale=1) {
            var data=selection.Range;var runs=data.Parts.Select(p=>ReadTextRun(p.Index)).ToList();
            Check(runs.All(r=>r!=null),UiText.Get("文字已变化，请重新选择。"));
            for(int i=0;i<selection.Indices.Length;i++)Check(ReadTextRun(selection.Indices[i]).Text==selection.Originals[i],UiText.Get("文字已变化，请重新选择。"));
            bool keep=options.Face=="保留原字体";
            for(int i=0;i<runs.Count;i++)runs[i].Text=runs[i].Text.Substring(0,data.Parts[i].Positions.Length);
            var output=action==1?new List<SelectedOutput>():LayoutSelectedText(selection,options,runs);
            var added=new List<IntPtr>();bool hasBounds=false;N.Rect result=selection.Bounds;
            foreach(var item in output) {
                if(keep)CheckOriginalCharacters(item.Style,item.Text);
                IntPtr obj=N.FPDFPageObj_CreateTextObj(Handle,item.Style.Font,item.Size);bool owned=true;
                try {
                    Check(obj!=IntPtr.Zero&&N.FPDFText_SetText(obj,item.Text)!=0,UiText.Get("无法设置文字。"));
                    var matrix=item.Style.Matrix;matrix.e=item.X;matrix.f=item.Y;
                    Check(N.FPDFPageObj_SetMatrix(obj,ref matrix)!=0,UiText.Get("无法设置文字位置。"));
                    var color=options.ChangeColor?options.Color:item.Style.Color;
                    N.FPDFPageObj_SetFillColor(obj,color.R,color.G,color.B,item.Style.Fill.A);
                    N.FPDFPageObj_SetStrokeColor(obj,color.R,color.G,color.B,item.Style.Stroke.A);
                    if(item.Positions.Length>0)Check(N.FPDFText_SetPositions(obj,item.Positions,new UIntPtr((uint)item.Positions.Length))!=0,UiText.Get("无法保留文字间距。"));
                    N.FPDFPage_InsertObject(Page,obj);owned=false;added.Add(obj);
                    if(action==2)N.FPDFPageObj_Transform(obj,scale,0,0,scale,selection.Bounds.left*(1-scale),selection.Bounds.top*(1-scale));
                    var b=ObjectBounds(obj);result=hasBounds?UnionTextBounds(result,b):b;hasBounds=true;
                }finally{if(owned&&obj!=IntPtr.Zero)N.FPDFPageObj_Destroy(obj);}
            }
            for(int i=0;i<runs.Count;i++){
                var run=runs[i];var part=data.Parts[i];string prefix=run.Text.Substring(0,part.From),suffix=run.Text.Substring(part.From+part.Count);
                string retained=prefix+suffix;
                if(retained.Length==0){Check(N.FPDFPage_RemoveObject(Page,run.Object)!=0,UiText.Get("无法删除原文字。"));N.FPDFPageObj_Destroy(run.Object);continue;}
                Check(N.FPDFText_SetText(run.Object,retained)!=0,UiText.Get("无法设置文字。"));
                int originalFirst=prefix.Length==0?part.From+part.Count:0;
                float startX=part.Positions[originalFirst];var matrix=run.Matrix;matrix.e+=matrix.a*startX;matrix.f+=matrix.b*startX;
                Check(N.FPDFPageObj_SetMatrix(run.Object,ref matrix)!=0,UiText.Get("无法设置文字位置。"));
                if(retained.Length>1){var positions=new float[retained.Length-1];for(int c=1;c<retained.Length;c++){int old=c<prefix.Length?c:c+part.Count;positions[c-1]=part.Positions[old]-startX;}Check(N.FPDFText_SetPositions(run.Object,positions,new UIntPtr((uint)positions.Length))!=0,UiText.Get("无法保留文字间距。"));}
            }
            CloseText();for(int i=0;i<added.Count;i++)Check(ObjectText(added[i]).TrimEnd()==output[i].Text.TrimEnd(),UiText.Get("原字体无法正确表示输入文字，请选择“自动”或其他系统字体后重试。"));
            options.ResultBounds=hasBounds?UnionTextBounds(selection.Bounds,result):selection.Bounds;
        }
        void VerifySelectedSave() {
            string expected=AllText();using(var verify=FromBytes(Serialize(false),Password))Check(verify.AllText()==expected,
                UiText.Get("此页字体保存后无法保持文字完整，已取消修改；可尝试明确更换字体。"));
        }
        bool ApplySelectedText(TextEditSelection selection,TextEditOptions options,bool preview) {
            options.ResultBounds=selection.Bounds;
            if(options.Text.Replace("\r","")==selection.Text.Replace("\r","")&&options.Face=="保留原字体"&&Near(options.Size,selection.Size)&&!options.ChangeColor&&!options.Wrap&&Near(options.LineHeight,selection.LineHeight))return false;
            int saved=Index;
            if(selection.Children==null){Edit(delegate{ReplaceSelectedCore(selection,options);},VerifySelectedSave);return true;}
            string requested=options.Text.Replace("\r","");
            var children=selection.Children;options.PageBounds=new N.Rect[children.Length];var contents=new List<string>();int at=0;
            for(int i=0;i<children.Length;i++){
                string marker=SelectionDivider(children[i].Page);Check(requested.IndexOf(marker,at,StringComparison.Ordinal)==at,UiText.Get("请保留跨页选区的页面分隔标记。"));at+=marker.Length;
                int end=i+1<children.Length?requested.IndexOf(SelectionDivider(children[i+1].Page),at,StringComparison.Ordinal):requested.Length;
                Check(end>=at,UiText.Get("请保留跨页选区的页面分隔标记。"));contents.Add(requested.Substring(at,end-at));at=end;
            }
            Edit(delegate{for(int i=0;i<children.Length;i++){OpenPage(preview?i:children[i].Page);var child=children[i];var opt=new TextEditOptions{Text=contents[i],Face=options.Face,Size=options.Face=="保留原字体"?child.Size*options.Size/selection.Size:options.Size*selection.SizeScale/child.SizeScale,Color=options.Color,ChangeColor=options.ChangeColor,Wrap=options.Wrap,Width=options.Width*selection.Scale/child.Scale,LineHeight=Near(options.LineHeight,selection.LineHeight)?child.LineHeight:options.LineHeight};ReplaceSelectedCore(child,opt);Check(N.FPDFPage_GenerateContent(Page)!=0,UiText.Get("无法生成页面内容。"));options.PageBounds[i]=opt.ResultBounds;if(i==0)options.ResultBounds=opt.ResultBounds;}OpenPage(saved);},VerifySelectedSave);
            return true;
        }
        internal void ModifySelectedText(List<TextRange> ranges,bool resize,float ratio) {
            Check(Finite(ratio)&&ratio>0&&ratio<=100,UiText.Get("请输入 0 到 10000 之间的百分比。"));
            int saved=Index;var selections=new List<TextEditSelection>();
            try{foreach(var range in ranges){OpenPage(range.Page);selections.Add(SelectTextRange(range));}}finally{OpenPage(saved);}
            Edit(delegate{foreach(var selected in selections){OpenPage(selected.Page);var options=new TextEditOptions{Text=selected.Text,Size=selected.Size,Color=selected.Color,Width=selected.Width,LineHeight=selected.LineHeight};ReplaceSelectedCore(selected,options,resize?2:1,ratio);Check(N.FPDFPage_GenerateContent(Page)!=0,UiText.Get("无法生成页面内容。"));}OpenPage(saved);},VerifySelectedSave);
        }
    }
}
