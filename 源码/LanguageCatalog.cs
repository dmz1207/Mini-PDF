using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniAI.Desktop {
    // Hy-MT2's published list includes Chinese variants and regional languages.
    // One catalog drives the service, main window, image view and reader menus.
    internal static class LanguageCatalog {
        static readonly string[] Entries={"zh|Chinese|简体中文","en|English|英语","zh-Hant|Traditional Chinese|繁體中文","ja|Japanese|日语","ko|Korean|韩语","fr|French|法语","de|German|德语","es|Spanish|西班牙语","pt|Portuguese|葡萄牙语","ru|Russian|俄语","it|Italian|意大利语","ar|Arabic|阿拉伯语","th|Thai|泰语","vi|Vietnamese|越南语","tr|Turkish|土耳其语","ms|Malay|马来语","id|Indonesian|印度尼西亚语","tl|Filipino|菲律宾语","hi|Hindi|印地语","pl|Polish|波兰语","cs|Czech|捷克语","nl|Dutch|荷兰语","km|Khmer|高棉语","my|Burmese|缅甸语","fa|Persian|波斯语","gu|Gujarati|古吉拉特语","ur|Urdu|乌尔都语","te|Telugu|泰卢固语","mr|Marathi|马拉地语","he|Hebrew|希伯来语","bn|Bengali|孟加拉语","ta|Tamil|泰米尔语","uk|Ukrainian|乌克兰语","bo|Tibetan|藏语","kk|Kazakh|哈萨克语","mn|Mongolian|蒙古语","ug|Uyghur|维吾尔语","yue|Cantonese|粤语"};
        internal static readonly string[] Codes=Entries.Select(x=>x.Split('|')[0]).ToArray();
        internal static readonly string[] EnglishLabels=Entries.Select(x=>x.Split('|')[1]).ToArray();
        internal static readonly string[] ChineseLabels=Entries.Select(x=>x.Split('|')[2]).ToArray();
        internal static readonly Dictionary<string,string> Names=Entries.ToDictionary(x=>x.Split('|')[0],x=>x.Split('|')[1],StringComparer.Ordinal);
        internal static string[] Labels(bool english){return english?EnglishLabels:ChineseLabels;}
        internal static bool IsRightToLeft(string code){return code=="ar"||code=="fa"||code=="ur"||code=="he"||code=="ug";}
    }
}
