using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Microsoft.Win32;

class Installer {
    const string RelWin64 = @"steamapps\common\MECCHA CHAMELEON\Chameleon\Binaries\Win64";

    static void C(string m, ConsoleColor col){ var p=Console.ForegroundColor; Console.ForegroundColor=col; Console.WriteLine(m); Console.ForegroundColor=p; }
    static void Info(string m){ C(m, ConsoleColor.Cyan); }
    static void Ok(string m){ C(m, ConsoleColor.Green); }
    static void Warn(string m){ C(m, ConsoleColor.Yellow); }
    static void Err(string m){ C(m, ConsoleColor.Red); }

    static List<string> SteamLibraries(){
        var libs = new List<string>();
        string steam = null;
        try { var k=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"); if(k!=null) steam=(string)k.GetValue("SteamPath"); } catch {}
        if(string.IsNullOrEmpty(steam)){ try { var k=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"); if(k!=null) steam=(string)k.GetValue("InstallPath"); } catch {} }
        if(!string.IsNullOrEmpty(steam)){
            steam = steam.Replace('/','\\');
            libs.Add(steam);
            foreach(var vdf in new[]{ Path.Combine(steam,@"steamapps\libraryfolders.vdf"), Path.Combine(steam,@"config\libraryfolders.vdf") }){
                if(File.Exists(vdf)){
                    string txt = File.ReadAllText(vdf);
                    foreach(Match m in Regex.Matches(txt, "\"path\"\\s*\"([^\"]+)\"")) libs.Add(m.Groups[1].Value.Replace("\\\\","\\"));
                }
            }
        }
        return libs;
    }

    static string FindGameWin64(){
        foreach(var lib in SteamLibraries()){ var p = Path.Combine(lib, RelWin64); if(Directory.Exists(p)) return p; }
        foreach(var d in DriveInfo.GetDrives()){
            if(!d.IsReady) continue;
            foreach(var g in new[]{"SteamLibrary","Steam",@"Games\Steam","SteamGames"}){
                var p = Path.Combine(d.RootDirectory.FullName, g, RelWin64); if(Directory.Exists(p)) return p;
            }
        }
        return null;
    }

    static void CopyDir(string src, string dst){
        Directory.CreateDirectory(dst);
        foreach(var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach(var s in Directory.GetDirectories(src)) CopyDir(s, Path.Combine(dst, Path.GetFileName(s)));
    }

    static int Pause(int code){ Console.WriteLine(); Console.Write("Press any key to close..."); try { Console.ReadKey(true); } catch {} return code; }

    static int Main(string[] args){
        try {
            Console.Title = "MECCHA CHAMELEON - No Decoy Death Installer";
            Info("=============================================="); Info("  MECCHA CHAMELEON  -  No Decoy Death installer"); Info("=============================================="); Console.WriteLine();
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string payload = Path.Combine(exeDir, "payload");
            if(!File.Exists(Path.Combine(payload,"dwmapi.dll"))){ Err("Payload folder missing. Keep this .exe together with the 'payload' folder."); return Pause(1); }

            Info("[1/4] Locating MECCHA CHAMELEON ...");
            string win64 = (args.Length>0 && Directory.Exists(args[0])) ? args[0] : FindGameWin64();
            if(win64 == null){
                Warn("Could not auto-detect the game.");
                Warn("In Steam: right-click the game > Manage > Browse local files, then paste the");
                Warn("folder path below (it ends in ...\\Chameleon\\Binaries\\Win64):");
                Console.Write("Path: ");
                string manual = (Console.ReadLine() ?? "").Trim().Trim('"');
                if(Directory.Exists(manual)){
                    if(!manual.EndsWith("Win64", StringComparison.OrdinalIgnoreCase)){ var t=Path.Combine(manual,@"Chameleon\Binaries\Win64"); if(Directory.Exists(t)) manual=t; }
                    win64 = manual;
                }
            }
            if(win64 == null || !Directory.Exists(win64)){ Err("Game folder not found. Aborting."); return Pause(1); }
            Ok("      Found: " + win64);

            Info("[2/4] Checking prerequisites ...");
            if(Process.GetProcessesByName("PenguinHotel-Win64-Shipping").Length > 0){ Err("The game is running. Please CLOSE it, then run this again."); return Pause(1); }
            string ue4ss = Path.Combine(win64, "ue4ss");
            bool fresh = !Directory.Exists(ue4ss);
            if(fresh) Ok("      Fresh install."); else Warn("      Existing UE4SS found -> merging (your other mods are kept).");

            Info("[3/4] Installing files ...");
            if(fresh){
                File.Copy(Path.Combine(payload,"dwmapi.dll"), Path.Combine(win64,"dwmapi.dll"), true);
                CopyDir(Path.Combine(payload,"ue4ss"), ue4ss);
            } else {
                if(!File.Exists(Path.Combine(win64,"dwmapi.dll"))) File.Copy(Path.Combine(payload,"dwmapi.dll"), Path.Combine(win64,"dwmapi.dll"), true);
                string modDst = Path.Combine(ue4ss, @"Mods\NoDecoyDeath");
                if(Directory.Exists(modDst)) Directory.Delete(modDst, true);
                CopyDir(Path.Combine(payload, @"ue4ss\Mods\NoDecoyDeath"), modDst);
                string sigDst = Path.Combine(ue4ss, "UE4SS_Signatures");
                Directory.CreateDirectory(sigDst);
                File.Copy(Path.Combine(payload, @"ue4ss\UE4SS_Signatures\StaticConstructObject.lua"), Path.Combine(sigDst,"StaticConstructObject.lua"), true);
                string modsTxt = Path.Combine(ue4ss, @"Mods\mods.txt");
                if(File.Exists(modsTxt)){
                    var lines = new List<string>(File.ReadAllLines(modsTxt));
                    bool found=false;
                    for(int i=0;i<lines.Count;i++) if(Regex.IsMatch(lines[i], @"^\s*NoDecoyDeath\s*:")){ lines[i]="NoDecoyDeath : 1"; found=true; }
                    if(!found){ int idx=lines.FindIndex(l=>l.Contains("Built-in keybinds")); if(idx>=0) lines.Insert(idx,"NoDecoyDeath : 1"); else lines.Add("NoDecoyDeath : 1"); }
                    File.WriteAllLines(modsTxt, lines);
                }
            }

            Info("[4/4] Verifying ...");
            bool okAll = true;
            foreach(var rel in new[]{"dwmapi.dll", @"ue4ss\UE4SS.dll", @"ue4ss\UE4SS_Signatures\StaticConstructObject.lua", @"ue4ss\Mods\NoDecoyDeath\Scripts\main.lua"})
                if(!File.Exists(Path.Combine(win64, rel))){ Err("      MISSING: " + rel); okAll=false; }
            Console.WriteLine();
            if(okAll){
                Ok("=============================================="); Ok("  INSTALLED. You're set as the HOST."); Ok("=============================================="); Console.WriteLine();
                Info("How it works:");
                Console.WriteLine("  - HOST a match as a Survivor. Only the host needs this mod.");
                Console.WriteLine("  - When a Hunter shoots your decoy, it POPS and you don't convert.");
                Console.WriteLine("  - In the lobby, press Shift+F7 to toggle protection ON / OFF.");
                Console.WriteLine("    (It locks once the match countdown starts, unlocks back in lobby.)");
                Console.WriteLine("  - After a GAME UPDATE, just run this installer again if it stops working.");
            } else Err("Install incomplete - see MISSING lines above.");
            return Pause(okAll?0:1);
        } catch(UnauthorizedAccessException){ Err("Access denied. Right-click this .exe and 'Run as administrator'."); return Pause(1); }
          catch(Exception ex){ Err("ERROR: " + ex.Message); return Pause(1); }
    }
}
