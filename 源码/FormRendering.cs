using System;
using System.Runtime.InteropServices;

namespace MiniPdf {
    internal static partial class N {
        [DllImport(D)] internal static extern int FPDF_GetFormType(IntPtr document);
        [DllImport(D)] internal static extern IntPtr FPDFDOC_InitFormFillEnvironment(IntPtr document,IntPtr info);
        [DllImport(D)] internal static extern void FPDFDOC_ExitFormFillEnvironment(IntPtr form);
        [DllImport(D)] internal static extern void FORM_OnAfterLoadPage(IntPtr page,IntPtr form);
        [DllImport(D)] internal static extern void FORM_OnBeforeClosePage(IntPtr page,IntPtr form);
        [DllImport(D)] internal static extern void FPDF_FFLDraw(IntPtr form,IntPtr bitmap,IntPtr page,int x,int y,int width,int height,int rotate,int flags);
    }
    // Drawing only: no document/page actions, JavaScript, network or submission callbacks.
    internal sealed class FormRendering : IDisposable {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr GetPage(IntPtr self,IntPtr document,int index);
        [StructLayout(LayoutKind.Sequential)]struct Info {
            internal int Version;
            internal IntPtr Release,Invalidate,Selected,Cursor,SetTimer,KillTimer,Time,Changed,Page,Current,Rotation,Named,Focus,Uri,Goto,Js;
        }
        IntPtr info,handle,page;readonly GetPage getPage;
        internal FormRendering(IntPtr document,IntPtr target,int index) {
            if(N.FPDF_GetFormType(document)!=1)return;page=target;
            getPage=delegate(IntPtr ignored,IntPtr doc,int requested){return requested==index?page:IntPtr.Zero;};
            var value=new Info{Version=1,Page=Marshal.GetFunctionPointerForDelegate(getPage)};
            info=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Info)));Marshal.StructureToPtr(value,info,false);
            handle=N.FPDFDOC_InitFormFillEnvironment(document,info);if(handle!=IntPtr.Zero)N.FORM_OnAfterLoadPage(page,handle);
        }
        internal void Draw(IntPtr bitmap,int x,int y,int width,int height,int rotation,int flags){if(handle!=IntPtr.Zero)N.FPDF_FFLDraw(handle,bitmap,page,x,y,width,height,rotation,flags);}
        public void Dispose(){if(handle!=IntPtr.Zero){N.FORM_OnBeforeClosePage(page,handle);N.FPDFDOC_ExitFormFillEnvironment(handle);handle=IntPtr.Zero;}if(info!=IntPtr.Zero){Marshal.FreeHGlobal(info);info=IntPtr.Zero;}GC.KeepAlive(getPage);}
    }
}
