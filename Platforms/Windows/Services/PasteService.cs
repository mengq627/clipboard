using System.Runtime.InteropServices;
using clipboard.Utils;

namespace clipboard.Platforms.Windows.Services;

/// <summary>
/// 粘贴服务，用于发送键盘输入来执行粘贴操作
/// </summary>
public class PasteService
{
    // Windows API 常量
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    
    // 扫描码 (Scan Codes)
    private const ushort SCANCODE_LSHIFT = 0x2A;  // 左 Shift 扫描码
    private const ushort SCANCODE_INSERT = 0x52;  // Insert 扫描码

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct INPUT
    {
        [FieldOffset(0)]
        public uint type;
        
        // 在 64 位系统上，联合体字段从偏移 8 开始（4 字节 type + 4 字节填充）
        // 在 32 位系统上，联合体字段从偏移 4 开始
        [FieldOffset(8)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] INPUT[] pInputs, int cbSize);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();

    /// <summary>
    /// 发送 Shift+Insert 组合键来执行粘贴操作
    /// </summary>
    public async void SendPaste()
    {
        try
        {
            DebugHelper.DebugWrite("Sending Shift+Insert for paste operation");
            
            // 获取额外的消息信息（用于 SendInput）
            IntPtr extraInfo = GetMessageExtraInfo();

            // 只考虑64位系统
            int inputSize = 40;
            
            // 使用扫描码的 Shift down
            var shiftDownScan = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,  // 使用扫描码时设为 0
                    wScan = SCANCODE_LSHIFT,
                    dwFlags = KEYEVENTF_SCANCODE,  // 使用扫描码标志
                    time = 0,
                    dwExtraInfo = extraInfo
                }
            };
            
            // 使用扫描码的 Insert down
            var insertDownScan = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,  // 使用扫描码时设为 0
                    wScan = SCANCODE_INSERT,
                    dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_EXTENDEDKEY,  // 使用扫描码标志
                    time = 0,
                    dwExtraInfo = extraInfo
                }
            };

            // 释放 Insert（使用扫描码）
            var insertUpScan = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = SCANCODE_INSERT,
                    dwFlags = KEYEVENTF_KEYUP | KEYEVENTF_SCANCODE | KEYEVENTF_EXTENDEDKEY,
                    time = 0,
                    dwExtraInfo = extraInfo
                }
            };

            // 释放 Shift（使用扫描码）
            var shiftUpScan = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = SCANCODE_LSHIFT,
                    dwFlags = KEYEVENTF_KEYUP | KEYEVENTF_SCANCODE,
                    time = 0,
                    dwExtraInfo = extraInfo
                }
            };

            var inputsDownScan = new[] { shiftDownScan, insertDownScan, insertUpScan, shiftUpScan };
            uint resultDownScan = SendInput(4, inputsDownScan, inputSize);
            DebugHelper.DebugWrite($"SendInput with scan codes, result: {resultDownScan}");
            
            if (resultDownScan != 4)
            {
                int errorCode = Marshal.GetLastWin32Error();
                DebugHelper.DebugWrite($"SendInput failed for keys: {errorCode}");
            }
        }
        catch (Exception ex)
        {
            DebugHelper.DebugWrite($"Error sending paste: {ex.Message}");
        }
    }
}

