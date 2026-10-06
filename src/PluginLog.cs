using System;
using System.IO;
using System.Reflection;
using BepInEx.Logging;

namespace SimplePlanes2MouseFlight
{
    internal enum PluginLogLevel
    {
        Off = 0,
        Normal = 1,
        Verbose = 2
    }

    // 日志分级包装。约定依据：共享文档 docs/config-and-logging.md 第二节。
    // 最低档 Off 必须做到一条都不输出（包括不创建日志文件）；调用点在拼字符串之前先看 IsNormal / IsVerbose。
    //
    // 除 BepInEx 日志之外，本插件再追加写入一份独立文件（与 DLL 同目录）。
    // 原因：BepInEx 的 LogOutput.log 默认每次启动覆盖、且不带时间戳，事后无法区分多次测试。
    internal sealed class PluginLog
    {
        private const string FileName = "SimplePlanes2MouseFlight.log";

        private readonly ManualLogSource _logger;
        private PluginLogLevel _level = PluginLogLevel.Normal;

        private bool _fileAttempted;
        private StreamWriter _file;
        private string _filePath;

        public PluginLog(ManualLogSource logger)
        {
            _logger = logger;
        }


        public bool IsNormal
        {
            get { return _level != PluginLogLevel.Off; }
        }

        public bool IsVerbose
        {
            get { return _level == PluginLogLevel.Verbose; }
        }

        public string FilePath
        {
            get { return _filePath; }
        }

        public void SetLevelText(string text)
        {
            if (text == null)
            {
                _level = PluginLogLevel.Normal;
                return;
            }

            string trimmed = text.Trim();
            if (string.Equals(trimmed, "Off", StringComparison.OrdinalIgnoreCase))
            {
                _level = PluginLogLevel.Off;
                return;
            }

            if (string.Equals(trimmed, "Verbose", StringComparison.OrdinalIgnoreCase))
            {
                _level = PluginLogLevel.Verbose;
                return;
            }

            if (string.Equals(trimmed, "Normal", StringComparison.OrdinalIgnoreCase) || trimmed.Length == 0)
            {
                _level = PluginLogLevel.Normal;
                return;
            }

            _level = PluginLogLevel.Normal;
            Warn("LogLevel 的值 \"" + text + "\" 无法识别，按 Normal 处理。可用：Off、Normal、Verbose。");
        }

        public void Info(string message)
        {
            if (_level == PluginLogLevel.Off)
            {
                return;
            }

            string line = Stamp() + "[INFO] " + message;
            _logger.LogInfo(line);
            WriteFile(line);
        }

        public void Warn(string message)
        {
            if (_level == PluginLogLevel.Off)
            {
                return;
            }

            string line = Stamp() + "[WARN] " + message;
            _logger.LogWarning(line);
            WriteFile(line);
        }

        public void Error(string message)
        {
            if (_level == PluginLogLevel.Off)
            {
                return;
            }

            string line = Stamp() + "[ERROR] " + message;
            _logger.LogError(line);
            WriteFile(line);
        }

        public void Verbose(string message)
        {
            if (_level != PluginLogLevel.Verbose)
            {
                return;
            }

            string line = Stamp() + "[VERBOSE] " + message;
            _logger.LogInfo(line);
            WriteFile(line);
        }

        public void Close()
        {
            if (_file == null)
            {
                return;
            }

            try
            {
                _file.WriteLine(Stamp() + "[INFO] 会话结束。");
                _file.Flush();
                _file.Dispose();
            }
            catch (Exception)
            {
                // 关文件失败无需处理。
            }

            _file = null;
        }

        private static string Stamp()
        {
            // 相对时间用于排序，本机时间用于和用户描述的操作时刻对上。
            return "[MF +" + UnityEngine.Time.realtimeSinceStartup.ToString("0.0") + "s "
                + DateTime.Now.ToString("HH:mm:ss") + "] ";
        }

        private void WriteFile(string line)
        {
            EnsureFile();

            if (_file == null)
            {
                return;
            }

            try
            {
                _file.WriteLine(line);
            }
            catch (Exception ex)
            {
                _filePath = null;
                _file = null;
                _logger.LogWarning("写独立日志文件失败，后续只写 BepInEx 日志：" + ex.GetType().Name);
            }
        }

        private void EnsureFile()
        {
            if (_fileAttempted)
            {
                return;
            }

            _fileAttempted = true;

            try
            {
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string directory = string.IsNullOrEmpty(dllPath) ? null : Path.GetDirectoryName(dllPath);

                if (string.IsNullOrEmpty(directory))
                {
                    // 少数加载方式下 Location 为空，退到 BepInEx 的插件目录。
                    directory = BepInEx.Paths.PluginPath;
                }

                if (string.IsNullOrEmpty(directory))
                {
                    return;
                }

                _filePath = Path.Combine(directory, FileName);
                _file = new StreamWriter(_filePath, true);
                _file.AutoFlush = true;
                _file.WriteLine();
                _file.WriteLine("===== 会话开始 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    + "　插件 " + PluginInfo.Version + "　日志级别 " + _level + " =====");
            }
            catch (Exception ex)
            {
                _filePath = null;
                _file = null;
                _logger.LogWarning("无法创建独立日志文件，只写 BepInEx 日志：" + ex.GetType().Name);
            }
        }
    }
}
