using System;
using System.Runtime.InteropServices;

public static class XNAWebRenderer
{
	public delegate void TextureUpdatedDelegate(IntPtr buffer);

	private const string nativeLibName = "XNAWebRenderer.dll";

	[DllImport("XNAWebRenderer.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern void XNAWR_Initialize([MarshalAs(UnmanagedType.LPStr)] string initialURL, TextureUpdatedDelegate callback, int width, int height);

	[DllImport("XNAWebRenderer.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern void XNAWR_Shutdown();

	[DllImport("XNAWebRenderer.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern void XNAWR_Update();

	[DllImport("XNAWebRenderer.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern void XNAWR_LoadURL([MarshalAs(UnmanagedType.LPStr)] string URL);

	[DllImport("XNAWebRenderer.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern void XNAWR_SetViewport(int width, int height);
}
