using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace NHi.Inventory.Services;

/// <summary>
/// Lists the machine's video-input devices by friendly name via the DirectShow system device
/// enumerator. This does NOT open any capture stream, so it's fast, doesn't flash the camera
/// light, and — unlike probing <c>VideoCapture(0..n)</c> — reliably reports a second/third
/// webcam. The enumeration order matches the index OpenCvSharp's DirectShow backend uses.
/// </summary>
public static class CameraEnumerator
{
    public sealed record CameraDevice(int Index, string Name);

    private static readonly Guid CLSID_SystemDeviceEnum = new("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
    private static readonly Guid CLSID_VideoInputDeviceCategory = new("860BB310-5D01-11d0-BD3B-00A0C911CE86");
    private static readonly Guid IID_IPropertyBag = new("55272A00-42CB-11CE-8135-00AA004BB851");

    public static List<CameraDevice> List()
    {
        var result = new List<CameraDevice>();
        object? devEnumObj = null;
        try
        {
            var t = Type.GetTypeFromCLSID(CLSID_SystemDeviceEnum, throwOnError: false);
            if (t == null) return result;
            devEnumObj = Activator.CreateInstance(t);
            if (devEnumObj is not ICreateDevEnum devEnum) return result;

            var cat = CLSID_VideoInputDeviceCategory;
            if (devEnum.CreateClassEnumerator(ref cat, out var enumMoniker, 0) != 0 || enumMoniker == null)
                return result;   // 1 = S_FALSE => no devices in this category

            var monikers = new IMoniker[1];
            int index = 0;
            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                var moniker = monikers[0];
                string name = ReadFriendlyName(moniker) ?? $"Camera {index + 1}";
                result.Add(new CameraDevice(index, name));
                Marshal.ReleaseComObject(moniker);
                index++;
            }
            Marshal.ReleaseComObject(enumMoniker);
        }
        catch
        {
            // Any COM hiccup -> caller falls back to plain index probing.
        }
        finally
        {
            if (devEnumObj != null) Marshal.ReleaseComObject(devEnumObj);
        }
        return result;
    }

    private static string? ReadFriendlyName(IMoniker moniker)
    {
        object? bagObj = null;
        try
        {
            var bagId = IID_IPropertyBag;
            moniker.BindToStorage(null!, null!, ref bagId, out bagObj);
            if (bagObj is not IPropertyBag bag) return null;

            object? value = null;
            if (bag.Read("FriendlyName", ref value, IntPtr.Zero) == 0 && value is string s && !string.IsNullOrWhiteSpace(s))
                return s.Trim();
            if (bag.Read("Description", ref value, IntPtr.Zero) == 0 && value is string d && !string.IsNullOrWhiteSpace(d))
                return d.Trim();
        }
        catch { /* fall through */ }
        finally
        {
            if (bagObj != null) Marshal.ReleaseComObject(bagObj);
        }
        return null;
    }

    [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateDevEnum
    {
        [PreserveSig]
        int CreateClassEnumerator([In] ref Guid pType, [Out] out IEnumMoniker ppEnumMoniker, [In] int dwFlags);
    }

    [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag
    {
        [PreserveSig]
        int Read([In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
                 [In, Out, MarshalAs(UnmanagedType.Struct)] ref object? pVar,
                 [In] IntPtr pErrorLog);

        [PreserveSig]
        int Write([In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
                  [In, MarshalAs(UnmanagedType.Struct)] ref object pVar);
    }
}
