param(
    [Parameter(Mandatory = $true)][ValidateSet('Tap', 'Hold', 'Swipe', 'Key', 'Text', 'SkinButton')][string]$Action,
    [int]$X,
    [int]$Y,
    [int]$ToX,
    [int]$ToY,
    [ValidateRange(60, 500)][int]$DurationMilliseconds = 300,
    [ValidateRange(600, 2000)][int]$HoldMilliseconds = 1000,
    [string]$ScreenshotPath,
    [string]$Key,
    [string]$TextFile,
    [ValidateSet('Back')][string]$SkinButton = 'Back',
    [string]$VirtualMachineName,
    [string]$CompilerPath = $env:MIXDROME_CSC,
    [ValidateRange(5, 60)][int]$TimeoutSeconds = 30,
    [switch]$CompileOnly
)

$ErrorActionPreference = 'Stop'
$phoneInputProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$phoneInputArgument = ''
switch ($Action) {
    { $_ -eq 'Tap' -or $_ -eq 'Hold' -or $_ -eq 'Swipe' } {
        if (-not $PSBoundParameters.ContainsKey('X') -or -not $PSBoundParameters.ContainsKey('Y') -or -not $ScreenshotPath) {
            throw "$Action requires -X, -Y and -ScreenshotPath from the observed guest display."
        }
        $phoneInputPng = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $ScreenshotPath).Path)
        if ($phoneInputPng.Length -lt 24 -or [BitConverter]::ToString($phoneInputPng, 0, 8) -ne '89-50-4E-47-0D-0A-1A-0A') {
            throw 'The observation must be a guest-display PNG screenshot.'
        }
        $phoneInputWidth = ([int]$phoneInputPng[16] -shl 24) -bor ([int]$phoneInputPng[17] -shl 16) -bor ([int]$phoneInputPng[18] -shl 8) -bor [int]$phoneInputPng[19]
        $phoneInputHeight = ([int]$phoneInputPng[20] -shl 24) -bor ([int]$phoneInputPng[21] -shl 16) -bor ([int]$phoneInputPng[22] -shl 8) -bor [int]$phoneInputPng[23]
        if ($X -lt 0 -or $Y -lt 0 -or $X -ge $phoneInputWidth -or $Y -ge $phoneInputHeight -or $X -gt [short]::MaxValue -or $Y -gt [short]::MaxValue) {
            throw "$Action coordinates must lie within the observed guest display."
        }
        $phoneInputArgument = "$X,$Y"
        if ($Action -eq 'Hold') { $phoneInputArgument = "$X,$Y,$HoldMilliseconds" }
        if ($Action -eq 'Swipe') {
            if (-not $PSBoundParameters.ContainsKey('ToX') -or -not $PSBoundParameters.ContainsKey('ToY')) {
                throw 'Swipe requires -ToX and -ToY within the observed guest display.'
            }
            if ($ToX -lt 0 -or $ToY -lt 0 -or $ToX -ge $phoneInputWidth -or $ToY -ge $phoneInputHeight -or $ToX -gt [short]::MaxValue -or $ToY -gt [short]::MaxValue) {
                throw 'Swipe destination coordinates must lie within the observed guest display.'
            }
            if ($X -eq $ToX -and $Y -eq $ToY) { throw 'Swipe requires distinct start and destination coordinates.' }
            $phoneInputArgument = "$X,$Y,$ToX,$ToY,$DurationMilliseconds"
        }
    }
    'Key' {
        if ([string]::IsNullOrWhiteSpace($Key)) { throw 'Key requires a key name or chord, such as Return or ControlKey+A.' }
        $phoneInputArgument = $Key
    }
    'Text' {
        if (-not $TextFile) { throw 'Text requires a private UTF-8 file; text is never accepted as a command argument.' }
        $phoneInputArgument = (Resolve-Path -LiteralPath $TextFile).Path
        if ((Get-Item -LiteralPath $phoneInputArgument).Length -gt 8192) { throw 'The input text file is too large.' }
    }
    'SkinButton' { $phoneInputArgument = $SkinButton }
}
if (-not $VirtualMachineName) {
    $phoneInputCandidates = @(Get-CimInstance Win32_Process -Filter "Name = 'XDE.exe'" |
        ForEach-Object {
            if ($_.CommandLine -match '(?:^|\s)/name\s+"([^"]+)"') { $Matches[1] }
        })
    if ($phoneInputCandidates.Count -ne 1) {
        throw 'Supply -VirtualMachineName to select exactly one running Windows Phone emulator.'
    }
    $VirtualMachineName = $phoneInputCandidates[0]
}
if (-not $CompilerPath) { $CompilerPath = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\14.0\Bin\csc.exe' }
if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw 'Supply -CompilerPath or MIXDROME_CSC for the .NET Framework C# compiler.'
}
$phoneInputGacRoot = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_32'
$phoneInputInterface = Join-Path $phoneInputGacRoot 'Microsoft.Xde.Interface\v4.0_8.1.0.0__31bf3856ad364e35\Microsoft.Xde.Interface.dll'
$phoneInputCommon = Join-Path $phoneInputGacRoot 'Microsoft.Xde.Common\v4.0_8.1.0.0__31bf3856ad364e35\Microsoft.Xde.Common.dll'
foreach ($phoneInputReference in @($phoneInputInterface, $phoneInputCommon)) {
    if (-not (Test-Path -LiteralPath $phoneInputReference -PathType Leaf)) {
        throw 'The Windows Phone 8.1 emulator automation SDK is required.'
    }
}
$phoneInputWorkRoot = Join-Path $phoneInputProjectRoot '.tmp\phone-emulator-input'
New-Item -ItemType Directory -Path $phoneInputWorkRoot -Force | Out-Null
$phoneInputSource = Join-Path $phoneInputWorkRoot 'EmulatorInput.cs'
$phoneInputExecutable = Join-Path $phoneInputWorkRoot 'emulator-input.exe'
@'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Xde.Common;
using Microsoft.Xde.Interface;

internal static class EmulatorInput
{
    // This is the source identifier used by the SDK's native-touch adapter.
    private const int EmulatorTouchSource = 222;

    private static void Contact(IXdeAutomationInputPipe pipe, short x, short y, InputEventFlag flags)
    {
        TouchContact contact = new TouchContact();
        contact.ContactId = 0;
        contact.ScreenX = contact.WindowX = x;
        contact.ScreenY = contact.WindowY = y;
        contact.Flags = flags;
        TouchInfo info = new TouchInfo(new[] { contact }, 1);
        info.Header.Source = EmulatorTouchSource;
        info.Header.TimeStamp = Environment.TickCount;
        pipe.SendTouchInfo(info);
    }

    private static Keys ParseKey(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "ctrl": case "control": name = "ControlKey"; break;
            case "shift": name = "ShiftKey"; break;
            case "alt": name = "Menu"; break;
            case "enter": name = "Return"; break;
            case "esc": name = "Escape"; break;
        }
        Keys key;
        if (!Enum.TryParse(name, true, out key) || !Enum.IsDefined(typeof(Keys), key) ||
            (int)key <= 0 || (int)key > ushort.MaxValue || key == Keys.LWin || key == Keys.RWin)
            throw new ArgumentException("Unsupported guest key name.");
        return key;
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3) throw new ArgumentException("Expected emulator, action and action argument.");
            using (IXdeAutomation emulator = AutomationClient.CreateAutomationClient(args[0]))
            {
                if (!emulator.IsShellReady || !emulator.InputPipe.Connected)
                    throw new InvalidOperationException("The emulator guest input pipe is not ready.");
                IXdeAutomationInputPipe pipe = emulator.InputPipe;
                switch (args[1])
                {
                    case "Tap":
                        string[] coordinates = args[2].Split(',');
                        short x = short.Parse(coordinates[0]);
                        short y = short.Parse(coordinates[1]);
                        try
                        {
                            Contact(pipe, x, y, InputEventFlag.Down | InputEventFlag.InRange);
                            Thread.Sleep(60);
                        }
                        finally { Contact(pipe, x, y, InputEventFlag.Up | InputEventFlag.Empty); }
                        break;
                    case "Swipe":
                        string[] swipeCoordinates = args[2].Split(',');
                        short swipeX = short.Parse(swipeCoordinates[0]);
                        short swipeY = short.Parse(swipeCoordinates[1]);
                        short toX = short.Parse(swipeCoordinates[2]);
                        short toY = short.Parse(swipeCoordinates[3]);
                        int duration = int.Parse(swipeCoordinates[4]);
                        if (duration < 60 || duration > 500) throw new ArgumentException("Unsupported swipe duration.");
                        short currentX = swipeX;
                        short currentY = swipeY;
                        const int steps = 12;
                        Stopwatch gesture = Stopwatch.StartNew();
                        try
                        {
                            Contact(pipe, swipeX, swipeY, InputEventFlag.Down | InputEventFlag.InRange);
                            for (int step = 1; step <= steps; step++)
                            {
                                int wait = duration * step / steps - (int)gesture.ElapsedMilliseconds;
                                if (wait > 0) Thread.Sleep(wait);
                                currentX = (short)Math.Round(swipeX + (toX - swipeX) * step / (double)steps);
                                currentY = (short)Math.Round(swipeY + (toY - swipeY) * step / (double)steps);
                                Contact(pipe, currentX, currentY, InputEventFlag.Move | InputEventFlag.InRange);
                            }
                        }
                        finally { Contact(pipe, currentX, currentY, InputEventFlag.Up | InputEventFlag.Empty); }
                        break;
                    case "Hold":
                        string[] holdCoordinates = args[2].Split(',');
                        if (holdCoordinates.Length != 3) throw new ArgumentException("Expected hold coordinates and duration.");
                        short holdX = short.Parse(holdCoordinates[0]);
                        short holdY = short.Parse(holdCoordinates[1]);
                        int holdMilliseconds = int.Parse(holdCoordinates[2]);
                        if (holdMilliseconds < 600 || holdMilliseconds > 2000) throw new ArgumentException("Unsupported hold duration.");
                        try
                        {
                            Contact(pipe, holdX, holdY, InputEventFlag.Down | InputEventFlag.InRange);
                            Thread.Sleep(holdMilliseconds);
                        }
                        finally { Contact(pipe, holdX, holdY, InputEventFlag.Up | InputEventFlag.Empty); }
                        break;
                    case "Key":
                        string[] names = args[2].Split('+');
                        List<Keys> keys = new List<Keys>();
                        foreach (string name in names) keys.Add(ParseKey(name.Trim()));
                        int pressed = 0;
                        try
                        {
                            foreach (Keys key in keys)
                            {
                                pressed++;
                                pipe.SendKeyboardEvent(0, key, true);
                            }
                        }
                        finally
                        {
                            for (int index = pressed - 1; index >= 0; index--)
                                pipe.SendKeyboardEvent(0, keys[index], false);
                        }
                        break;
                    case "Text":
                        string text = File.ReadAllText(args[2], Encoding.UTF8);
                        if (text.Length > 2048) throw new ArgumentException("The input text is too long.");
                        pipe.SendText(text);
                        break;
                    case "SkinButton":
                        if (args[2] != "Back") throw new ArgumentException("Only the guest Back button is supported.");
                        emulator.PressSkinButton(SkinButtonType.Back);
                        break;
                    default: throw new ArgumentException("Unsupported action.");
                }
            }
            return 0;
        }
        catch (Exception error)
        {
            // Never include typed text or authentication-bearing URLs in output.
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }
}
'@ | Set-Content -LiteralPath $phoneInputSource -Encoding utf8NoBOM
& $CompilerPath /nologo /platform:x86 "/out:$phoneInputExecutable" $phoneInputSource "/r:$phoneInputInterface" "/r:$phoneInputCommon" /r:System.ServiceModel.dll /r:System.Windows.Forms.dll
if ($LASTEXITCODE -ne 0) { throw 'Guest input SDK compilation failed.' }
if ($CompileOnly) {
    [pscustomobject]@{ Compiled = $true; InputSent = $false; Executable = $phoneInputExecutable } | ConvertTo-Json
    return
}
$phoneInputStartInfo = [Diagnostics.ProcessStartInfo]::new($phoneInputExecutable)
$phoneInputStartInfo.UseShellExecute = $false
$phoneInputStartInfo.CreateNoWindow = $true
$phoneInputStartInfo.RedirectStandardError = $true
$phoneInputStartInfo.ArgumentList.Add($VirtualMachineName)
$phoneInputStartInfo.ArgumentList.Add($Action)
$phoneInputStartInfo.ArgumentList.Add($phoneInputArgument)
$phoneInputProcess = [Diagnostics.Process]::Start($phoneInputStartInfo)
try {
    if (-not $phoneInputProcess.WaitForExit($TimeoutSeconds * 1000)) {
        $phoneInputProcess.Kill()
        $phoneInputProcess.WaitForExit()
        throw "Guest input SDK timed out after $TimeoutSeconds seconds; inspect a fresh capture before another action."
    }
    $phoneInputError = $phoneInputProcess.StandardError.ReadToEnd()
    if ($phoneInputProcess.ExitCode -ne 0) { throw "Guest input failed: $phoneInputError" }
} finally { $phoneInputProcess.Dispose() }
[pscustomobject]@{
    VirtualMachineName = $VirtualMachineName
    Action = $Action
    InputSent = $true
    Verification = 'Capture the guest display before the next action.'
} | ConvertTo-Json
