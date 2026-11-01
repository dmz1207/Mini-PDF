// Developer-only integration runner; excluded from the reader executable.
using System;
using System.Collections;
using System.IO;
using System.Reflection;

internal static class PortableAiTest {
    static int count;
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);count++;Console.WriteLine("PASS: "+label);}
    static int Main(string[] args) {
        string output=Path.GetFullPath(args[2]),root=Path.Combine(output,"MiniAI");Type client=null;var flags=BindingFlags.Static|BindingFlags.NonPublic;
        try {
            Directory.CreateDirectory(output);File.Copy(args[1],Path.Combine(output,"MiniAI-Portable.exe"),true);
            Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",Path.Combine(output,"Mini PDF.exe"));
            client=Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("MiniPdf.MiniAiClient",true);
            Check(!Directory.Exists(root),"reader starts with only the single EXE and no extracted package");
            Check((bool)client.GetProperty("Available",flags).GetValue(null,null),"reader enables optional AI features from the portable EXE");
            client.GetMethod("Ensure",flags).Invoke(null,new object[]{root});
            var health=(IDictionary)client.GetMethod("Request",flags).Invoke(null,new object[]{root,"/health",null,10,null});
            Check((bool)health["ocr"]&&(bool)health["translation"],"reader automatically prepares and starts both offline capabilities");
            string translated=(string)client.GetMethod("TranslateText",flags).Invoke(null,new object[]{root,"The control system remains stable.","zh",null});
            Check(translated.Contains("控制")&&translated.Contains("稳定"),"reader translates through the automatically prepared single EXE");
            Console.WriteLine("Assertions: "+count);return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{if(client!=null)try{client.GetMethod("Request",flags).Invoke(null,new object[]{root,"/v1/shutdown",new{stop=true},5,null});}catch(Exception){}}
    }
}
