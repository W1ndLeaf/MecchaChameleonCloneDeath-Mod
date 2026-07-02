using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Microsoft.Win32;

class Uninstaller {
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

    static int Pause(int code){ Console.WriteLine(); Console.Write("Press any key to close..."); try { Console.ReadKey(true); } catch {} return code; }

    static int Main(string[] args){
        try {
            Console.Title = "MECCHA CHAMELEON - No Decoy Death Uninstaller";
            Info("MECCHA CHAMELEON  -  No Decoy Death uninstaller"); Console.WriteLine();
            string win64 = (args.Length>0 && Directory.Exists(args[0])) ? args[0] : FindGameWin64();
            if(win64 == null){ Console.Write("Game Win64 path: "); string m=(Console.ReadLine()??"").Trim().Trim('"'); if(Directory.Exists(m)) win64=m; }
            if(win64 == null || !Directory.Exists(win64)){ Err("Game folder not found."); return Pause(1); }
            if(Process.GetProcessesByName("PenguinHotel-Win64-Shipping").Length > 0){ Err("Close the game first, then run this again."); return Pause(1); }

            string ue4ss = Path.Combine(win64, "ue4ss");
            string mod   = Path.Combine(ue4ss, @"Mods\NoDecoyDeath");
            if(Directory.Exists(mod)){ Directory.Delete(mod, true); Ok("Removed Mods\\NoDecoyDeath."); } else Warn("NoDecoyDeath mod folder not found.");

            string modsTxt = Path.Combine(ue4ss, @"Mods\mods.txt");
            if(File.Exists(modsTxt)){
                var lines = File.ReadAllLines(modsTxt);
                for(int i=0;i<lines.Length;i++) if(Regex.IsMatch(lines[i], @"^\s*NoDecoyDeath\s*:")) lines[i]="NoDecoyDeath : 0";
                File.WriteAllLines(modsTxt, lines);
                Ok("Disabled NoDecoyDeath in mods.txt.");
            }
            Console.WriteLine();
            Ok("Done - the decoy mod is removed.");
            Warn("UE4SS itself was left installed (in case you use other mods). To remove it fully, delete:");
            Warn("   " + Path.Combine(win64,"dwmapi.dll"));
            Warn("   " + ue4ss);
            Warn("(or in Steam: right-click the game > Properties > Installed Files > Verify integrity).");
            return Pause(0);
        } catch(UnauthorizedAccessException){ Err("Access denied. Right-click this .exe and 'Run as administrator'."); return Pause(1); }
          catch(Exception ex){ Err("ERROR: " + ex.Message); return Pause(1); }
    }
}
