using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FinalTacticsCompatibility
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string gameDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            LauncherCore core = new LauncherCore(gameDir);
            bool helperMode = CommandLine.Has(args, "/borderless-helper") || CommandLine.Has(args, "-borderless-helper");

            try
            {
                if (helperMode)
                {
                    NativeMethods.EnableDpiAwarenessForHelper();
                    return core.RunBorderlessHelper(args);
                }

                if (CommandLine.Has(args, "/selftest") || CommandLine.Has(args, "-selftest"))
                {
                    LauncherSettings settings = core.LoadSettings();
                    CommandLine.ApplyOverrides(args, settings);
                    core.UseGameDir(settings.GameDir);
                    core.WriteSelfTestReport();
                    return core.RequiredFilesMissing().Count == 0 ? 0 : 1;
                }

                if (CommandLine.Has(args, "/cleanup") || CommandLine.Has(args, "-cleanup"))
                {
                    core.StopGameProcesses();
                    return 0;
                }

                if (CommandLine.Has(args, "/apply") || CommandLine.Has(args, "-apply"))
                {
                    LauncherSettings settings = core.LoadSettings();
                    CommandLine.ApplyOverrides(args, settings);
                    core.SaveSettings(settings);
                    core.ApplySettings(settings);
                    return 0;
                }

                if (CommandLine.Has(args, "/restore") || CommandLine.Has(args, "-restore"))
                {
                    LauncherSettings settings = core.LoadSettings();
                    CommandLine.ApplyOverrides(args, settings);
                    core.SaveSettings(settings);
                    core.UseGameDir(settings.GameDir);
                    core.RestoreOriginal();
                    return 0;
                }

                if (CommandLine.Has(args, "/launch") || CommandLine.Has(args, "-launch"))
                {
                    LauncherSettings settings = core.LoadSettings();
                    CommandLine.ApplyOverrides(args, settings);
                    core.SaveSettings(settings);
                    core.ApplySettings(settings);
                    core.StopGameProcesses();
                    core.StartGame(false, settings);
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LauncherForm(core));
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(core.AnalysisDir);
                    File.WriteAllText(Path.Combine(core.AnalysisDir, "FinalTacticsLauncher.error.txt"), ex.ToString(), Encoding.UTF8);
                }
                catch
                {
                }
                if (!helperMode)
                {
                    MessageBox.Show(ex.Message, "Final Tactics Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return 1;
            }
        }
    }

    internal sealed class LauncherForm : Form
    {
        private readonly LauncherCore core;
        private readonly TextBox gameDirBox;
        private readonly ComboBox profileBox;
        private readonly ComboBox resolutionBox;
        private readonly ComboBox bppBox;
        private readonly ComboBox displayModeBox;
        private readonly ComboBox fpsBox;
        private readonly ComboBox dpiBox;
        private readonly Label profileDescription;
        private readonly Label displayDescription;
        private readonly Label fpsDescription;
        private readonly Label dpiDescription;
        private readonly Label patchDescription;
        private readonly Label statusLabel;
        private readonly Dictionary<string, int> profileIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> resolutionIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> displayIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> fpsIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> dpiIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private bool isUpdating;

        public LauncherForm(LauncherCore core)
        {
            this.core = core;
            Font = new Font("Malgun Gothic", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Text = "파이널 택틱스 설정";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(720, 584);
            BackColor = Color.FromArgb(248, 249, 251);

            Label title = new Label();
            title.Text = "파이널 택틱스 설정";
            title.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
            title.Location = new Point(18, 16);
            title.Size = new Size(300, 28);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "게임 폴더를 선택하고 화면, 속도, 진행 패치를 한 번에 적용합니다.";
            subtitle.ForeColor = Color.FromArgb(85, 91, 102);
            subtitle.Location = new Point(20, 47);
            subtitle.Size = new Size(670, 24);
            Controls.Add(subtitle);

            Label gameDirLabel = new Label();
            gameDirLabel.Text = "게임 폴더";
            gameDirLabel.Location = new Point(20, 82);
            gameDirLabel.Size = new Size(100, 22);
            gameDirLabel.ForeColor = Color.FromArgb(47, 54, 66);
            Controls.Add(gameDirLabel);

            gameDirBox = new TextBox();
            gameDirBox.Location = new Point(20, 107);
            gameDirBox.Size = new Size(574, 23);
            Controls.Add(gameDirBox);

            Button browseButton = AddButton("찾기", 604, 105, 86);
            browseButton.Click += delegate { BrowseGameFolder(); };

            profileBox = AddCombo("렌더링 방식", 20, 150, 298);
            profileDescription = AddHintLabel(338, 175, 352, 44);

            resolutionBox = AddCombo("렌더 해상도", 20, 228, 298);
            displayModeBox = AddCombo("표시 방식", 338, 228, 216);
            bppBox = AddCombo("색상", 574, 228, 116);

            displayDescription = AddHintLabel(20, 285, 670, 36);

            fpsBox = AddCombo("속도 제한", 20, 345, 170);
            fpsDescription = AddHintLabel(208, 370, 482, 36);

            dpiBox = AddCombo("DPI 처리", 20, 415, 170);
            dpiDescription = AddHintLabel(208, 440, 482, 36);

            patchDescription = AddHintLabel(20, 488, 670, 34);

            statusLabel = new Label();
            statusLabel.Location = new Point(20, 528);
            statusLabel.Size = new Size(670, 24);
            statusLabel.ForeColor = Color.FromArgb(55, 63, 74);
            Controls.Add(statusLabel);

            Button applyButton = AddButton("적용", 20, 550, 90);
            applyButton.Click += delegate { ApplySelected(false); };

            Button launchButton = AddButton("실행", 118, 550, 90);
            launchButton.Click += delegate { ApplySelected(true); };

            Button setupButton = AddButton("게임 설정", 216, 550, 100);
            setupButton.Click += delegate { StartOriginalSetup(); };

            Button cleanupButton = AddButton("프로세스 정리", 324, 550, 116);
            cleanupButton.Click += delegate { CleanupProcesses(); };

            Button restoreButton = AddButton("원본 복구", 448, 550, 100);
            restoreButton.Click += delegate { RestoreOriginal(); };

            Button closeButton = AddButton("닫기", 600, 550, 90);
            closeButton.Click += delegate { Close(); };

            PopulateCombos();
            LoadInitialSelection();
            UpdateDescriptions();
            RefreshStatus();
        }

        private ComboBox AddCombo(string labelText, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = labelText;
            label.Location = new Point(x, y);
            label.Size = new Size(width, 22);
            label.ForeColor = Color.FromArgb(47, 54, 66);
            Controls.Add(label);

            ComboBox box = new ComboBox();
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.Location = new Point(x, y + 25);
            box.Size = new Size(width, 25);
            box.SelectedIndexChanged += delegate
            {
                if (!isUpdating)
                {
                    UpdateDescriptions();
                }
            };
            Controls.Add(box);
            return box;
        }

        private Label AddHintLabel(int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.ForeColor = Color.FromArgb(88, 96, 110);
            Controls.Add(label);
            return label;
        }

        private Button AddButton(string text, int x, int y, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 30);
            button.UseVisualStyleBackColor = true;
            Controls.Add(button);
            return button;
        }

        private void PopulateCombos()
        {
            isUpdating = true;
            AddProfile("render_upscale", "고해상도 렌더링");
            AddProfile("bicubic", "부드러운 확대");
            AddProfile("lanczos", "선명한 확대");
            AddProfile("integer", "픽셀 보존");
            AddProfile("render_upscale_msaa4x", "고해상도 + MSAA");
            AddProfile("baseline", "보정 최소화");

            foreach (ResolutionOption option in core.Resolutions)
            {
                resolutionIndex[option.Key] = resolutionBox.Items.Count;
                resolutionBox.Items.Add(option.Label);
            }

            AddDisplayMode("fullscreen", "고해상도 전체화면");
            AddDisplayMode("original_fullscreen", "원본 스케일 전체화면");
            AddDisplayMode("windowed", "창 모드");
            AddDisplayMode("current", "현재 설정 유지");

            AddFps("30", "30 FPS");
            AddFps("60", "60 FPS");
            AddFps("off", "제한 없음");

            AddDpiMode("permonitor", "고DPI 직접 처리");
            AddDpiMode("unaware", "Windows 확대 처리");
            AddDpiMode("system", "시스템 DPI 기준");
            AddDpiMode("app", "게임 기본 처리");
            isUpdating = false;
        }

        private void AddProfile(string key, string label)
        {
            profileIndex[key] = profileBox.Items.Count;
            profileBox.Items.Add(label);
        }

        private void AddDisplayMode(string key, string label)
        {
            displayIndex[key] = displayModeBox.Items.Count;
            displayModeBox.Items.Add(label);
        }

        private void AddFps(string key, string label)
        {
            fpsIndex[key] = fpsBox.Items.Count;
            fpsBox.Items.Add(label);
        }

        private void AddDpiMode(string key, string label)
        {
            dpiIndex[key] = dpiBox.Items.Count;
            dpiBox.Items.Add(label);
        }

        private void LoadInitialSelection()
        {
            LauncherSettings settings = core.LoadSettings();
            gameDirBox.Text = settings.GameDir;
            SelectOrDefault(profileBox, profileIndex, settings.Profile, "render_upscale");
            SelectOrDefault(resolutionBox, resolutionIndex, core.NormalizeResolutionKey(settings.Resolution), "auto4x3");
            SelectOrDefault(displayModeBox, displayIndex, core.NormalizeDisplayMode(settings.DisplayMode), "fullscreen");
            SelectOrDefault(fpsBox, fpsIndex, settings.FpsLimit, "30");
            SelectOrDefault(dpiBox, dpiIndex, core.NormalizeDpiMode(settings.DpiMode), "permonitor");
            bppBox.Items.Add("16");
            bppBox.Items.Add("32");
            bppBox.SelectedItem = settings.Bpp == 16 ? "16" : "32";
        }

        private void SelectOrDefault(ComboBox box, Dictionary<string, int> map, string requested, string fallback)
        {
            int index;
            if (!map.TryGetValue(requested, out index))
            {
                index = map[fallback];
            }
            box.SelectedIndex = index;
        }

        private LauncherSettings GetSelectedSettings()
        {
            LauncherSettings settings = new LauncherSettings();
            settings.GameDir = gameDirBox.Text.Trim();
            settings.Profile = KeyByIndex(profileIndex, profileBox.SelectedIndex);
            settings.Resolution = KeyByIndex(resolutionIndex, resolutionBox.SelectedIndex);
            settings.Bpp = bppBox.SelectedItem != null && bppBox.SelectedItem.ToString() == "16" ? 16 : 32;
            settings.DisplayMode = KeyByIndex(displayIndex, displayModeBox.SelectedIndex);
            settings.FpsLimit = KeyByIndex(fpsIndex, fpsBox.SelectedIndex);
            settings.DpiMode = KeyByIndex(dpiIndex, dpiBox.SelectedIndex);
            settings.ApplyEnginePatches = true;
            settings.PrepareStartupAssets = true;
            return settings;
        }

        private string KeyByIndex(Dictionary<string, int> map, int index)
        {
            foreach (KeyValuePair<string, int> pair in map)
            {
                if (pair.Value == index)
                {
                    return pair.Key;
                }
            }
            return string.Empty;
        }

        private void UpdateDescriptions()
        {
            string profile = KeyByIndex(profileIndex, profileBox.SelectedIndex);
            string displayMode = KeyByIndex(displayIndex, displayModeBox.SelectedIndex);
            string fps = KeyByIndex(fpsIndex, fpsBox.SelectedIndex);
            string dpi = KeyByIndex(dpiIndex, dpiBox.SelectedIndex);

            profileDescription.Text = core.GetProfileDescription(profile);
            displayDescription.Text = core.GetDisplayModeDescription(displayMode);
            fpsDescription.Text = core.GetFpsDescription(fps);
            dpiDescription.Text = core.GetDpiDescription(dpi);
            patchDescription.Text = "적용 시 초반 이벤트 진행, 대사 입력, 시작 보정, 실행 호환성 파일을 함께 준비합니다. 기존 호환성 파일은 보존합니다.";
        }

        private void BrowseGameFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "파이널 택틱스가 설치된 폴더를 선택하세요.";
                dialog.SelectedPath = Directory.Exists(gameDirBox.Text) ? gameDirBox.Text : core.GameDir;
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    gameDirBox.Text = dialog.SelectedPath;
                    core.UseGameDir(dialog.SelectedPath);
                    RefreshStatus();
                }
            }
        }

        private void ApplySelected(bool launch)
        {
            try
            {
                LauncherSettings settings = GetSelectedSettings();
                core.SaveSettings(settings);
                core.ApplySettings(settings);
                RefreshStatus();

                if (launch)
                {
                    core.StopGameProcesses();
                    WindowState = FormWindowState.Minimized;
                    core.StartGame(false, settings);
                }
                else
                {
                    MessageBox.Show("선택한 설정을 적용했습니다.", "파이널 택틱스", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "설정 적용 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartOriginalSetup()
        {
            try
            {
                core.UseGameDir(gameDirBox.Text);
                core.StopGameProcesses();
                core.RequestOriginalSetup();
                WindowState = FormWindowState.Minimized;
                core.StartGame(true);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                WindowState = FormWindowState.Normal;
                MessageBox.Show(ex.Message, "원본 설정 실행 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CleanupProcesses()
        {
            try
            {
                core.UseGameDir(gameDirBox.Text);
                int stopped = core.StopGameProcesses();
                RefreshStatus();
                MessageBox.Show(stopped == 0 ? "정리할 실행 프로세스가 없습니다." : string.Format("실행 프로세스 {0}개를 정리했습니다.", stopped), "프로세스 정리", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "프로세스 정리 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreOriginal()
        {
            try
            {
                core.UseGameDir(gameDirBox.Text);
                DialogResult result = MessageBox.Show(
                    "FT.exe에 적용한 진행/입력/시작 보정을 원본 바이트로 되돌리고, 런처가 생성한 실행 설정 파일을 정리합니다.\r\n\r\n음악 폴더와 기존 호환성 파일은 삭제하지 않습니다. 계속할까요?",
                    "원본 복구",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    return;
                }

                core.StopGameProcesses();
                string message = core.RestoreOriginal();
                RefreshStatus();
                MessageBox.Show(message, "원본 복구", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "원본 복구 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshStatus()
        {
            if (gameDirBox != null && !string.IsNullOrWhiteSpace(gameDirBox.Text))
            {
                core.UseGameDir(gameDirBox.Text);
            }
            statusLabel.Text = core.GetStatusText();
        }
    }

    internal sealed class LauncherCore
    {
        private const string RegistrySubKey = @"Software\BAROQUE\FinalTactics\System";
        private readonly string launcherDir;
        private readonly string stateDir;
        private string gameDir;
        private readonly string launcherIni;

        public LauncherCore(string launcherDir)
        {
            this.launcherDir = NormalizeDirectory(launcherDir);
            stateDir = this.launcherDir;
            gameDir = DetectDefaultGameDir(this.launcherDir);
            AnalysisDir = BuildAnalysisDir(gameDir);
            launcherIni = Path.Combine(stateDir, "FinalTacticsLauncher.ini");
            Resolutions = BuildResolutionOptions();
        }

        public string AnalysisDir { get; private set; }
        public string GameDir { get { return gameDir; } }
        public List<ResolutionOption> Resolutions { get; private set; }

        private string FtExe { get { return Path.Combine(gameDir, "FT.exe"); } }
        private string DDrawCompatIni { get { return Path.Combine(gameDir, "DDrawCompat-FT.ini"); } }
        private string DDrawCompatSource { get { return Path.Combine(launcherDir, "ddraw.dll"); } }
        private string DSetupSource { get { return Path.Combine(launcherDir, "DSETUP.DLL"); } }
        private const string DDrawCompatResource = "FinalTacticsCompatibility.ddraw.dll";
        private const string DSetupResource = "FinalTacticsCompatibility.DSETUP.DLL";

        public void UseGameDir(string requestedDir)
        {
            gameDir = NormalizeDirectory(string.IsNullOrWhiteSpace(requestedDir) ? launcherDir : requestedDir);
            AnalysisDir = BuildAnalysisDir(gameDir);
        }

        private string BuildAnalysisDir(string candidateGameDir)
        {
            if (!string.IsNullOrWhiteSpace(candidateGameDir) && File.Exists(Path.Combine(candidateGameDir, "FT.exe")))
            {
                return Path.Combine(candidateGameDir, "analysis_out");
            }
            return Path.Combine(stateDir, "analysis_out");
        }

        private static string NormalizeDirectory(string value)
        {
            string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim()));
            char[] trimChars = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            string root = Path.GetPathRoot(fullPath);
            string trimmed = fullPath.TrimEnd(trimChars);
            return trimmed.Length == 0 ? root : trimmed;
        }

        private static string DetectDefaultGameDir(string launcherDir)
        {
            if (File.Exists(Path.Combine(launcherDir, "FT.exe")))
            {
                return launcherDir;
            }

            string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "InterSoft", "Final Tactics");
            if (File.Exists(Path.Combine(installed, "FT.exe")))
            {
                return NormalizeDirectory(installed);
            }

            return launcherDir;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "1" || normalized == "true" || normalized == "yes" || normalized == "on")
            {
                return true;
            }
            if (normalized == "0" || normalized == "false" || normalized == "no" || normalized == "off")
            {
                return false;
            }
            return fallback;
        }

        public List<string> RequiredFilesMissing()
        {
            string[] required =
            {
                FtExe,
                Path.Combine(gameDir, "game00.dat"),
                Path.Combine(gameDir, "game01.dat")
            };

            List<string> missing = new List<string>();
            foreach (string path in required)
            {
                if (!File.Exists(path))
                {
                    missing.Add(path);
                }
            }
            return missing;
        }

        public LauncherSettings LoadSettings()
        {
            LauncherSettings settings = LauncherSettings.Default();
            settings.GameDir = gameDir;
            string settingsPath = launcherIni;
            if (!File.Exists(settingsPath))
            {
                string legacyPath = Path.Combine(launcherDir, "FinalTacticsLauncher.ini");
                if (File.Exists(legacyPath))
                {
                    settingsPath = legacyPath;
                }
            }

            if (!File.Exists(settingsPath))
            {
                return settings;
            }

            foreach (string rawLine in File.ReadAllLines(settingsPath, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("[") || line.StartsWith(";"))
                {
                    continue;
                }

                int pos = line.IndexOf('=');
                if (pos < 0)
                {
                    continue;
                }

                string key = line.Substring(0, pos).Trim();
                string value = line.Substring(pos + 1).Trim();
                if (key.Equals("game_dir", StringComparison.OrdinalIgnoreCase))
                {
                    settings.GameDir = value;
                }
                else if (key.Equals("profile", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Profile = value;
                }
                else if (key.Equals("resolution", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Resolution = value;
                }
                else if (key.Equals("bpp", StringComparison.OrdinalIgnoreCase))
                {
                    int bpp;
                    if (int.TryParse(value, out bpp) && (bpp == 16 || bpp == 32))
                    {
                        settings.Bpp = bpp;
                    }
                }
                else if (key.Equals("display_mode", StringComparison.OrdinalIgnoreCase))
                {
                    settings.DisplayMode = value;
                }
                else if (key.Equals("fps_limit", StringComparison.OrdinalIgnoreCase))
                {
                    settings.FpsLimit = value;
                }
                else if (key.Equals("dpi_mode", StringComparison.OrdinalIgnoreCase))
                {
                    settings.DpiMode = value;
                }
                else if (key.Equals("apply_engine_patches", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ApplyEnginePatches = ParseBool(value, settings.ApplyEnginePatches);
                }
                else if (key.Equals("prepare_startup_assets", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("install_local_music", StringComparison.OrdinalIgnoreCase))
                {
                    settings.PrepareStartupAssets = ParseBool(value, settings.PrepareStartupAssets);
                }
            }

            UseGameDir(settings.GameDir);
            settings.GameDir = gameDir;
            settings.Resolution = NormalizeResolutionKey(settings.Resolution);
            settings.DisplayMode = NormalizeDisplayMode(settings.DisplayMode);
            settings.DpiMode = NormalizeDpiMode(settings.DpiMode);
            if (!HasResolution(settings.Resolution))
            {
                settings.Resolution = "auto4x3";
            }
            return settings;
        }

        public void SaveSettings(LauncherSettings settings)
        {
            Directory.CreateDirectory(stateDir);
            settings.GameDir = NormalizeDirectory(string.IsNullOrWhiteSpace(settings.GameDir) ? gameDir : settings.GameDir);
            string[] lines =
            {
                "[launcher]",
                "game_dir=" + settings.GameDir,
                "profile=" + settings.Profile,
                "resolution=" + NormalizeResolutionKey(settings.Resolution),
                "bpp=" + settings.Bpp.ToString(),
                "display_mode=" + NormalizeDisplayMode(settings.DisplayMode),
                "fps_limit=" + settings.FpsLimit,
                "dpi_mode=" + NormalizeDpiMode(settings.DpiMode),
                "apply_engine_patches=" + (settings.ApplyEnginePatches ? "1" : "0"),
                "prepare_startup_assets=" + (settings.PrepareStartupAssets ? "1" : "0")
            };
            File.WriteAllLines(launcherIni, lines, new UTF8Encoding(false));
        }

        public void ApplySettings(LauncherSettings settings)
        {
            UseGameDir(settings.GameDir);
            List<string> missing = RequiredFilesMissing();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException("필수 파일을 찾을 수 없습니다.\r\n\r\n" + string.Join("\r\n", missing.ToArray()));
            }

            settings.Resolution = NormalizeResolutionKey(settings.Resolution);
            settings.DisplayMode = NormalizeDisplayMode(settings.DisplayMode);
            ResolutionOption resolution = ResolveApplyResolution(settings);
            BackupRegistry();
            BackupDDrawCompatConfig(settings.Profile);
            InstallSupportFiles();
            if (settings.PrepareStartupAssets)
            {
                PrepareStartupAssets();
            }
            EnsureDisplayFilesAvailable(settings);
            ApplyFtPatches(settings.ApplyEnginePatches, settings.PrepareStartupAssets);
            WriteDDrawCompatConfig(settings.Profile, settings.FpsLimit, settings.DpiMode);
            WriteGameRegistry(settings, resolution);
        }

        private void InstallSupportFiles()
        {
            InstallSupportFile(DDrawCompatSource, Path.Combine(gameDir, "ddraw.dll"), "ddraw.dll", DDrawCompatResource);
            InstallSupportFile(DSetupSource, Path.Combine(gameDir, "DSETUP.DLL"), "DSETUP.DLL", DSetupResource);
        }

        private void InstallSupportFile(string source, string destination, string name, string resourceName)
        {
            if (ShouldPreserveExistingSupportFile(destination))
            {
                return;
            }

            if (File.Exists(source) && Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (File.Exists(destination) && SupportFileEqualsSource(source, resourceName, destination))
            {
                return;
            }

            if (File.Exists(destination))
            {
                BackupFile(destination, name + ".before_launcher");
            }
            CopySupportFile(source, resourceName, destination, name);
        }

        private bool ShouldPreserveExistingSupportFile(string destination)
        {
            if (!File.Exists(destination))
            {
                return false;
            }

            return File.Exists(Path.Combine(gameDir, "dgVoodoo.conf")) ||
                   File.Exists(Path.Combine(gameDir, "D3DImm.dll")) ||
                   Directory.Exists(Path.Combine(gameDir, "DosBox")) ||
                   Directory.Exists(Path.Combine(gameDir, "DOOGIE"));
        }

        private void CopySupportFile(string source, string resourceName, string destination, string name)
        {
            if (File.Exists(source))
            {
                File.Copy(source, destination, true);
                return;
            }

            byte[] embedded = ReadEmbeddedResource(resourceName, name);
            File.WriteAllBytes(destination, embedded);
        }

        private bool SupportFileEqualsSource(string source, string resourceName, string destination)
        {
            if (File.Exists(source))
            {
                return FilesEqual(source, destination);
            }

            byte[] embedded = ReadEmbeddedResource(resourceName, Path.GetFileName(destination));
            return FileBytesEqual(destination, embedded);
        }

        private bool HasEmbeddedResource(string resourceName)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Any(name => name.Equals(resourceName, StringComparison.OrdinalIgnoreCase));
        }

        private byte[] ReadEmbeddedResource(string resourceName, string displayName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException("런처 실행 파일에서 실행 호환성 파일을 찾을 수 없습니다.", displayName);
                }

                byte[] bytes = new byte[stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0)
                    {
                        break;
                    }
                    offset += read;
                }
                return bytes;
            }
        }

        private void PrepareStartupAssets()
        {
            string destination = Path.Combine(gameDir, "music");
            if (HasMusicFiles(destination))
            {
                return;
            }

            string source = FindMusicSource();
            if (source == null)
            {
                throw new InvalidOperationException("음악 파일을 찾을 수 없습니다. 게임 원본의 Music 폴더를 런처 폴더 옆에 두거나 원본 매체를 연결한 뒤 다시 적용하세요.");
            }

            CopyDirectory(source, destination);
        }

        private string FindMusicSource()
        {
            string[] candidates =
            {
                Path.Combine(launcherDir, "music"),
                Path.Combine(launcherDir, "Music"),
                Path.Combine(gameDir, "Music")
            };

            foreach (string candidate in candidates)
            {
                if (HasMusicFiles(candidate))
                {
                    return candidate;
                }
            }

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady)
                    {
                        continue;
                    }

                    string candidate = Path.Combine(drive.RootDirectory.FullName, "Music");
                    if (HasMusicFiles(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        private static bool HasMusicFiles(string path)
        {
            return Directory.Exists(path) &&
                   File.Exists(Path.Combine(path, "bgm01.wav")) &&
                   File.Exists(Path.Combine(path, "bgm24.wav"));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private void EnsureDisplayFilesAvailable(LauncherSettings settings)
        {
            string dsetup = Path.Combine(gameDir, "DSETUP.DLL");
            if (!File.Exists(dsetup))
            {
                throw new FileNotFoundException("DSETUP.DLL을 찾을 수 없습니다. 런처 폴더의 실행 호환성 파일 구성을 확인한 뒤 다시 적용하세요.", dsetup);
            }

            if (NeedsDisplayConfig(settings) && !File.Exists(Path.Combine(gameDir, "ddraw.dll")))
            {
                throw new FileNotFoundException("화면/속도 설정에 필요한 파일을 찾을 수 없습니다. 런처 폴더의 실행 호환성 파일 구성을 확인한 뒤 다시 적용하세요.", Path.Combine(gameDir, "ddraw.dll"));
            }
        }

        private bool NeedsDisplayConfig(LauncherSettings settings)
        {
            return !(settings.Profile == "baseline" && settings.FpsLimit == "off" && NormalizeDpiMode(settings.DpiMode) == "permonitor");
        }

        private void ApplyFtPatches(bool applyProgressPatches, bool applyStartupPatch)
        {
            if (!applyProgressPatches && !applyStartupPatch)
            {
                return;
            }

            byte[] bytes = File.ReadAllBytes(FtExe);
            bool changed = false;

            if (applyProgressPatches)
            {
                changed |= ApplyAnyBytePatch(bytes, "오프닝 영상 열기", GetMovieOpenGuardPatches());
                changed |= ApplyAnyBytePatch(bytes, "오프닝 영상 대기", GetMoviePatches());
                changed |= ApplyAnyBytePatch(bytes, "초반 이벤트 진행", GetFirstEventPatches());

                changed |= ApplyInputLatchPatch(bytes);
            }

            if (applyStartupPatch)
            {
                changed |= ApplyStartupCheckPatch(bytes);
            }

            if (changed)
            {
                BackupFile(FtExe, "FT.exe.before_launcher_patches");
                File.WriteAllBytes(FtExe, bytes);
            }
        }

        private bool ApplyStartupCheckPatch(byte[] bytes)
        {
            return ApplyAnyBytePatch(bytes, "시작 보정", GetStartupCheckPatches());
        }

        public string RestoreOriginal()
        {
            if (!File.Exists(FtExe))
            {
                throw new FileNotFoundException("FT.exe를 찾을 수 없습니다.", FtExe);
            }

            List<string> actions = new List<string>();
            List<string> notes = new List<string>();
            byte[] bytes = File.ReadAllBytes(FtExe);
            bool ftChanged = false;

            ftChanged |= RestoreAnyBytePatch(bytes, "오프닝 영상 열기", GetMovieOpenGuardPatches(), actions, notes);
            ftChanged |= RestoreAnyBytePatch(bytes, "오프닝 영상 대기", GetMoviePatches(), actions, notes);
            ftChanged |= RestoreAnyBytePatch(bytes, "초반 이벤트 진행", GetFirstEventPatches(), actions, notes);
            ftChanged |= RestoreInputLatchPatch(bytes, actions, notes);
            ftChanged |= RestoreAnyBytePatch(bytes, "시작 보정", GetStartupCheckPatches(), actions, notes);

            if (ftChanged)
            {
                BackupFile(FtExe, "FT.exe.before_original_restore");
                File.WriteAllBytes(FtExe, bytes);
                actions.Add("FT.exe의 런처 패치를 원본 바이트로 복구했습니다.");
            }
            else
            {
                actions.Add("FT.exe에서 복구할 런처 패치를 찾지 못했습니다.");
            }

            RestoreGeneratedDDrawCompatConfig(actions, notes);
            RestoreSupportFile("ddraw.dll", DDrawCompatSource, DDrawCompatResource, actions, notes);
            RestoreSupportFile("DSETUP.DLL", DSetupSource, DSetupResource, actions, notes);
            notes.Add("음악 폴더는 원본 CD 데이터일 수 있어 삭제하지 않았습니다.");

            List<string> lines = new List<string>();
            lines.AddRange(actions);
            if (notes.Count > 0)
            {
                lines.Add("");
                lines.AddRange(notes);
            }
            return string.Join("\r\n", lines.ToArray());
        }

        private static BytePatch[] GetMovieOpenGuardPatches()
        {
            return new BytePatch[]
            {
                new BytePatch("순정", 0x175DEA,
                    new byte[] { 0x75, 0x08 },
                    new byte[] { 0x90, 0x90 }),
                new BytePatch("변형", 0x1762DA,
                    new byte[] { 0x75, 0x08 },
                    new byte[] { 0x90, 0x90 })
            };
        }

        private static BytePatch[] GetMoviePatches()
        {
            return new BytePatch[]
            {
                new BytePatch("순정", 0x17B891,
                    new byte[] { 0xe8, 0x5e, 0xbb, 0xff, 0xff },
                    new byte[] { 0xe9, 0x81, 0x07, 0x00, 0x00 }),
                new BytePatch("변형", 0x17BD81,
                    new byte[] { 0xe8, 0x5e, 0xbb, 0xff, 0xff },
                    new byte[] { 0xe9, 0x81, 0x07, 0x00, 0x00 })
            };
        }

        private static BytePatch[] GetFirstEventPatches()
        {
            return new BytePatch[]
            {
                new BytePatch("순정", 0x17CAB4,
                    new byte[] { 0x75 },
                    new byte[] { 0xeb }),
                new BytePatch("변형", 0x17CFA4,
                    new byte[] { 0x75 },
                    new byte[] { 0xeb })
            };
        }

        private static BytePatch[] GetStartupCheckPatches()
        {
            return new BytePatch[]
            {
                new BytePatch("순정", 0x1aa119,
                    new byte[] { 0x55, 0x8b, 0xec, 0x81, 0xec, 0x9c, 0x04, 0x00, 0x00 },
                    new byte[] { 0xb8, 0x01, 0x00, 0x00, 0x00, 0xc3, 0x90, 0x90, 0x90 }),
                new BytePatch("변형", 0x1aaac9,
                    new byte[] { 0x55, 0x8b, 0xec, 0x81, 0xec, 0x9c, 0x04, 0x00, 0x00 },
                    new byte[] { 0xb8, 0x01, 0x00, 0x00, 0x00, 0xc3, 0x90, 0x90, 0x90 })
            };
        }

        private bool ApplyBytePatch(byte[] bytes, int offset, byte[] original, byte[] patched, string name)
        {
            if (BytesMatch(bytes, offset, patched))
            {
                return false;
            }
            if (!BytesMatch(bytes, offset, original))
            {
                throw new InvalidOperationException(name + " 패치 위치의 바이트가 예상과 다릅니다. 다른 버전의 FT.exe일 수 있어 적용을 중단합니다.");
            }

            Array.Copy(patched, 0, bytes, offset, patched.Length);
            return true;
        }

        private bool ApplyAnyBytePatch(byte[] bytes, string name, BytePatch[] variants)
        {
            foreach (BytePatch variant in variants)
            {
                if (BytesMatch(bytes, variant.Offset, variant.Patched))
                {
                    return false;
                }
            }

            foreach (BytePatch variant in variants)
            {
                if (BytesMatch(bytes, variant.Offset, variant.Original))
                {
                    Array.Copy(variant.Patched, 0, bytes, variant.Offset, variant.Patched.Length);
                    return true;
                }
            }

            throw new InvalidOperationException(name + " 패치 위치의 바이트가 예상과 다릅니다. 지원하지 않는 FT.exe 변형이거나 이미 다른 패치가 적용된 상태입니다.");
        }

        private bool ApplyInputLatchPatch(byte[] bytes)
        {
            InputLatchPatch[] variants = GetInputLatchPatches();

            foreach (InputLatchPatch variant in variants)
            {
                bool entryPatched = BytesMatch(bytes, variant.EntryOffset, variant.PatchedEntry);
                bool hookPatched = BytesMatch(bytes, variant.CaveOffset, variant.Hook);
                if (entryPatched && hookPatched)
                {
                    return false;
                }
                if (entryPatched || hookPatched)
                {
                    throw new InvalidOperationException("대사 선입력 패치가 일부만 적용된 상태입니다. 백업을 확인한 뒤 다시 시도하세요.");
                }
            }

            foreach (InputLatchPatch variant in variants)
            {
                if (!BytesMatch(bytes, variant.EntryOffset, variant.Original))
                {
                    continue;
                }
                if (!IsZeroRange(bytes, variant.CaveOffset, 96))
                {
                    throw new InvalidOperationException("대사 선입력 패치에 사용할 빈 코드 영역이 비어 있지 않습니다. 지원하지 않는 FT.exe 변형일 수 있어 적용을 중단합니다.");
                }

                Array.Copy(variant.Hook, 0, bytes, variant.CaveOffset, variant.Hook.Length);
                Array.Copy(variant.PatchedEntry, 0, bytes, variant.EntryOffset, variant.PatchedEntry.Length);
                return true;
            }

            throw new InvalidOperationException("대사 선입력 패치 위치의 바이트가 예상과 다릅니다. 지원하지 않는 FT.exe 변형일 수 있어 적용을 중단합니다.");
        }

        private bool RestoreAnyBytePatch(byte[] bytes, string name, BytePatch[] variants, List<string> actions, List<string> notes)
        {
            foreach (BytePatch variant in variants)
            {
                if (BytesMatch(bytes, variant.Offset, variant.Patched))
                {
                    Array.Copy(variant.Original, 0, bytes, variant.Offset, variant.Original.Length);
                    actions.Add(name + " 패치를 복구했습니다.");
                    return true;
                }
            }

            foreach (BytePatch variant in variants)
            {
                if (BytesMatch(bytes, variant.Offset, variant.Original))
                {
                    return false;
                }
            }

            notes.Add(name + " 위치가 알려진 원본/패치 바이트와 달라 건너뛰었습니다.");
            return false;
        }

        private bool RestoreInputLatchPatch(byte[] bytes, List<string> actions, List<string> notes)
        {
            InputLatchPatch[] variants = GetInputLatchPatches();
            foreach (InputLatchPatch variant in variants)
            {
                bool entryPatched = BytesMatch(bytes, variant.EntryOffset, variant.PatchedEntry);
                bool hookPatched = BytesMatch(bytes, variant.CaveOffset, variant.Hook);
                if (entryPatched && hookPatched)
                {
                    Array.Copy(variant.Original, 0, bytes, variant.EntryOffset, variant.Original.Length);
                    ClearRange(bytes, variant.CaveOffset, 96);
                    actions.Add("대사 선입력 패치를 복구했습니다.");
                    return true;
                }
                if (entryPatched || hookPatched)
                {
                    notes.Add("대사 선입력 패치가 일부만 적용된 상태라 자동 복구를 건너뛰었습니다.");
                    return false;
                }
            }

            foreach (InputLatchPatch variant in variants)
            {
                if (BytesMatch(bytes, variant.EntryOffset, variant.Original))
                {
                    return false;
                }
            }

            notes.Add("대사 선입력 위치가 알려진 원본/패치 바이트와 달라 건너뛰었습니다.");
            return false;
        }

        private static InputLatchPatch[] GetInputLatchPatches()
        {
            return new InputLatchPatch[]
            {
                BuildStockInputLatchPatch(),
                BuildCompatibleVariantInputLatchPatch()
            };
        }

        private static InputLatchPatch BuildStockInputLatchPatch()
        {
            return new InputLatchPatch(
                "순정",
                0x17dc11,
                0x1b37ed,
                new byte[]
                {
                    0x55,0x8b,0xec,
                    0xc7,0x05,0xf4,0x28,0x77,0x00,0x01,0x00,0x00,0x00,
                    0xa1,0x50,0x13,0x5e,0x00,
                    0xa3,0xa0,0x26,0x77,0x00,
                    0xc7,0x05,0x50,0x13,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0x5d,0xc3
                },
                BuildJumpEntry(0xd7, 0x5b, 0x03, 0x00),
                new byte[]
                {
                    0x55,0x8b,0xec,
                    0xc7,0x05,0x9c,0x38,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0xc7,0x05,0xa0,0x38,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0xc7,0x05,0xa4,0x38,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0xc7,0x05,0xa8,0x38,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0xc7,0x05,0xf4,0x28,0x77,0x00,0x01,0x00,0x00,0x00,
                    0xa1,0x50,0x13,0x5e,0x00,
                    0xa3,0xa0,0x26,0x77,0x00,
                    0xc7,0x05,0x50,0x13,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0x5d,0xc3
                });
        }

        private static InputLatchPatch BuildCompatibleVariantInputLatchPatch()
        {
            return new InputLatchPatch(
                "변형",
                0x17e101,
                0x1b47ed,
                new byte[]
                {
                    0x55,0x8b,0xec,
                    0xc7,0x05,0xe4,0x39,0x77,0x00,0x01,0x00,0x00,0x00,
                    0xa1,0x40,0x24,0x5e,0x00,
                    0xa3,0x90,0x37,0x77,0x00,
                    0xc7,0x05,0x40,0x24,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0x5d,0xc3
                },
                BuildJumpEntry(0xe7, 0x66, 0x03, 0x00),
                new byte[]
                {
                    0x55,0x8b,0xec,
                    0xc7,0x05,0x8c,0x49,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0xc7,0x05,0x90,0x49,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0xc7,0x05,0x94,0x49,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0xc7,0x05,0x98,0x49,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0xc7,0x05,0xe4,0x39,0x77,0x00,0x01,0x00,0x00,0x00,
                    0xa1,0x40,0x24,0x5e,0x00,
                    0xa3,0x90,0x37,0x77,0x00,
                    0xc7,0x05,0x40,0x24,0x5e,0x00,0x00,0x00,0x00,0x00,
                    0x5d,0xc3
                });
        }

        private static byte[] BuildJumpEntry(byte b0, byte b1, byte b2, byte b3)
        {
            byte[] bytes = new byte[35];
            bytes[0] = 0xe9;
            bytes[1] = b0;
            bytes[2] = b1;
            bytes[3] = b2;
            bytes[4] = b3;
            for (int i = 5; i < bytes.Length; i++)
            {
                bytes[i] = 0x90;
            }
            return bytes;
        }

        public void RequestOriginalSetup()
        {
            BackupRegistry();
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistrySubKey))
            {
                key.SetValue("SafetyComplete", 0, RegistryValueKind.DWord);
            }
        }

        public void StartGame(bool originalSetup)
        {
            StartGame(originalSetup, null);
        }

        public void StartGame(bool originalSetup, LauncherSettings settings)
        {
            if (settings != null)
            {
                UseGameDir(settings.GameDir);
            }
            if (!File.Exists(FtExe))
            {
                throw new FileNotFoundException("FT.exe를 찾을 수 없습니다.", FtExe);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = FtExe;
            startInfo.WorkingDirectory = gameDir;
            startInfo.UseShellExecute = false;
            Process process = Process.Start(startInfo);
            if (process != null)
            {
                NativeMethods.AllowSetForegroundWindow(process.Id);
            }
        }

        public int RunBorderlessHelper(string[] args)
        {
            try
            {
                string value;
                int processId;
                int width;
                int height;
                if (!CommandLine.TryGet(args, "-Pid", out value) || !int.TryParse(value, out processId))
                {
                    return 1;
                }
                if (!CommandLine.TryGet(args, "-Width", out value) || !int.TryParse(value, out width))
                {
                    return 1;
                }
                if (!CommandLine.TryGet(args, "-Height", out value) || !int.TryParse(value, out height))
                {
                    return 1;
                }

                Process process = Process.GetProcessById(processId);
                Thread.Sleep(3000);
                ApplyBorderlessWindow(process, width, height);
                return 0;
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(AnalysisDir);
                File.WriteAllText(Path.Combine(AnalysisDir, "FinalTacticsLauncher.borderless-helper.error.txt"), ex.ToString(), Encoding.UTF8);
                return 1;
            }
        }

        public int StopGameProcesses()
        {
            int stopped = 0;
            foreach (int pid in GetGameProcessIds().Concat(GetBorderlessHelperProcessIds()).Distinct())
            {
                try
                {
                    Process process = Process.GetProcessById(pid);
                    process.Kill();
                    if (!process.WaitForExit(3000))
                    {
                        process.WaitForExit(1000);
                    }
                    stopped++;
                }
                catch
                {
                }
            }
            return stopped;
        }

        public string GetStatusText()
        {
            try
            {
                if (!Directory.Exists(gameDir))
                {
                    return "게임 폴더를 찾을 수 없습니다.";
                }
                if (!File.Exists(FtExe))
                {
                    return "선택한 폴더에서 FT.exe를 찾을 수 없습니다.";
                }

                string patchStatus = GetProgressPatchStatus();
                string startupStatus = GetStartupCheckStatus();
                string musicStatus = HasMusicFiles(Path.Combine(gameDir, "music")) ? "음악 준비됨" : "음악 복사 필요";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistrySubKey))
                {
                    if (key == null)
                    {
                        return string.Format("게임 설정이 아직 없습니다. 진행 패치 {0}, 시작 보정 {1}, {2}", patchStatus, startupStatus, musicStatus);
                    }

                    int width = ReadInt(key, "ScreenWidth", 0);
                    int height = ReadInt(key, "ScreenHeight", 0);
                    int bpp = ReadInt(key, "ScreenBpp", 0);
                    int fullScreen = ReadInt(key, "FullScreen", 0);
                    string fps = ReadFpsLimiter();
                    string dpi = ReadDpiAwareness();
                    string display = FormatDisplayStatus(fullScreen);
                    return string.Format("현재 적용: {0} x {1} x {2}, {3}, FPS {4}, DPI {5}, 진행 패치 {6}, 시작 보정 {7}, {8}", width, height, bpp, display, fps, dpi, patchStatus, startupStatus, musicStatus);
                }
            }
            catch
            {
                return "현재 적용 상태를 읽을 수 없습니다.";
            }
        }

        private string GetProgressPatchStatus()
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(FtExe);
                bool movieOpen = BytesMatch(bytes, 0x175DEA, new byte[] { 0x90, 0x90 }) ||
                                 BytesMatch(bytes, 0x1762DA, new byte[] { 0x90, 0x90 });
                bool movie = BytesMatch(bytes, 0x17B891, new byte[] { 0xe9, 0x81, 0x07, 0x00, 0x00 }) ||
                             BytesMatch(bytes, 0x17BD81, new byte[] { 0xe9, 0x81, 0x07, 0x00, 0x00 });
                bool firstEvent = BytesMatch(bytes, 0x17CAB4, new byte[] { 0xeb }) ||
                                  BytesMatch(bytes, 0x17CFA4, new byte[] { 0xeb });
                bool inputLatch = (BytesMatch(bytes, 0x17dc11, new byte[] { 0xe9, 0xd7, 0x5b, 0x03, 0x00 }) &&
                                   BytesMatch(bytes, 0x1b37ed, new byte[] { 0x55, 0x8b, 0xec, 0xc7, 0x05, 0x9c, 0x38, 0x5e, 0x00 })) ||
                                  (BytesMatch(bytes, 0x17e101, new byte[] { 0xe9, 0xe7, 0x66, 0x03, 0x00 }) &&
                                   BytesMatch(bytes, 0x1b47ed, new byte[] { 0x55, 0x8b, 0xec, 0xc7, 0x05, 0x8c, 0x49, 0x5e, 0x00 }));
                if (movieOpen && movie && firstEvent && inputLatch)
                {
                    return "적용됨";
                }
                return "필요";
            }
            catch
            {
                return "확인 필요";
            }
        }

        private string GetStartupCheckStatus()
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(FtExe);
                bool startupPatch = BytesMatch(bytes, 0x1aa119, new byte[] { 0xb8, 0x01, 0x00, 0x00, 0x00, 0xc3, 0x90, 0x90, 0x90 }) ||
                                    BytesMatch(bytes, 0x1aaac9, new byte[] { 0xb8, 0x01, 0x00, 0x00, 0x00, 0xc3, 0x90, 0x90, 0x90 });
                return startupPatch ? "적용됨" : "필요";
            }
            catch
            {
                return "확인 필요";
            }
        }

        private string FormatDisplayStatus(int fullScreen)
        {
            if (fullScreen == 0)
            {
                return "창 모드";
            }

            string savedDisplayMode = NormalizeDisplayMode(LoadSettings().DisplayMode);
            if (savedDisplayMode == "original_fullscreen")
            {
                return "원본 스케일 전체화면";
            }
            return "고해상도 전체화면";
        }

        public string NormalizeResolutionKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "auto4x3";
            }

            string trimmed = value.Trim();
            if (trimmed.Equals("auto4x3", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("current", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.ToLowerInvariant();
            }

            string[] parts = trimmed.ToLowerInvariant().Split('x');
            int width;
            int height;
            if (parts.Length == 2 && int.TryParse(parts[0], out width) && int.TryParse(parts[1], out height))
            {
                return string.Format("{0}x{1}", width, height);
            }

            return trimmed;
        }

        public string NormalizeDisplayMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "fullscreen";
            }

            string trimmed = value.Trim().ToLowerInvariant();
            if (trimmed == "borderless_window")
            {
                return "fullscreen";
            }
            if (trimmed == "fullscreen" || trimmed == "original_fullscreen" || trimmed == "windowed" || trimmed == "current")
            {
                return trimmed;
            }

            return "fullscreen";
        }

        public string NormalizeDpiMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "permonitor";
            }

            string trimmed = value.Trim().ToLowerInvariant();
            if (trimmed == "app" || trimmed == "unaware" || trimmed == "system" || trimmed == "permonitor")
            {
                return trimmed;
            }

            return "permonitor";
        }

        public string GetProfileDescription(string profile)
        {
            if (profile == "render_upscale")
            {
                return "해상도를 높여 렌더하고 화면에 맞춰 표시합니다. 현재 가장 균형이 좋습니다.";
            }
            if (profile == "bicubic")
            {
                return "원본 해상도로 렌더한 뒤 부드럽게 확대합니다.";
            }
            if (profile == "lanczos")
            {
                return "원본 해상도를 유지하면서 가장자리를 더 선명하게 확대합니다.";
            }
            if (profile == "integer")
            {
                return "픽셀 형태를 보존합니다. 화면 가장자리에 여백이 생길 수 있습니다.";
            }
            if (profile == "render_upscale_msaa4x")
            {
                return "고해상도 렌더링에 MSAA 4x를 더합니다. 일부 효과는 추가 확인이 필요합니다.";
            }
            return "화면 보정을 최소화합니다. 문제 원인을 분리할 때 사용합니다.";
        }

        public string GetDisplayModeDescription(string displayMode)
        {
            if (displayMode == "fullscreen")
            {
                return "게임을 전체화면으로 실행하고 현재 화면에 맞춰 안정적으로 표시합니다.";
            }
            if (displayMode == "original_fullscreen")
            {
                return "게임은 원본 640 x 480으로 실행하고 최종 화면만 확대합니다.";
            }
            if (displayMode == "windowed")
            {
                return "일반 창 모드로 실행합니다. 높은 렌더 해상도에서는 창 크기가 화면보다 클 수 있습니다.";
            }
            if (displayMode == "current")
            {
                return "현재 게임 레지스트리의 표시 방식을 유지합니다.";
            }
            return "고해상도 전체 화면을 borderless 방식으로 표시합니다.";
        }

        public string GetFpsDescription(string fps)
        {
            if (fps == "60")
            {
                return "더 빠른 표시를 원할 때 사용합니다. 이벤트 속도는 직접 확인이 필요합니다.";
            }
            if (fps == "off")
            {
                return "제한을 해제합니다. 현대 환경에서는 게임 진행이 너무 빨라질 수 있습니다.";
            }
            return "렌더와 이벤트 속도가 과도하게 빨라지는 것을 막습니다.";
        }

        public string GetDpiDescription(string dpiMode)
        {
            dpiMode = NormalizeDpiMode(dpiMode);
            if (dpiMode == "unaware")
            {
                return "Windows가 창을 확대합니다. 창 모드 글자와 포인터 크기 비교용입니다.";
            }
            if (dpiMode == "system")
            {
                return "시작 시점의 시스템 DPI를 기준으로 둡니다. 다중 모니터 이동은 확인이 필요합니다.";
            }
            if (dpiMode == "app")
            {
                return "DPI 처리를 게임과 Windows에 맡깁니다. 원인 분리용입니다.";
            }
            return "고해상도 환경에서 글자와 포인터 크기 차이를 줄입니다.";
        }

        public void WriteSelfTestReport()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("self-test ok");
            builder.AppendLine("game_dir=" + gameDir);
            DesktopSize desktop = GetPrimaryDesktopSize();
            ResolutionOption auto = ResolveResolution("auto4x3");
            builder.AppendLine(string.Format("desktop={0}x{1}", desktop.Width, desktop.Height));
            builder.AppendLine(string.Format("auto4x3={0}x{1}", auto.Width, auto.Height));
            builder.AppendLine("available_4x3=" + string.Join(", ", Resolutions.Where(r => r.IsConcrete).Select(r => r.Key).ToArray()));
            builder.AppendLine("embedded_ddraw=" + HasEmbeddedResource(DDrawCompatResource));
            builder.AppendLine("embedded_dsetup=" + HasEmbeddedResource(DSetupResource));
            List<string> missing = RequiredFilesMissing();
            if (missing.Count > 0)
            {
                builder.AppendLine("missing=" + string.Join(", ", missing.ToArray()));
            }
            builder.AppendLine("settings_dir=" + stateDir);
            builder.AppendLine("analysis_dir=" + AnalysisDir);
            try
            {
                Directory.CreateDirectory(AnalysisDir);
                File.WriteAllText(Path.Combine(AnalysisDir, "FinalTacticsLauncher.selftest.txt"), builder.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private bool HasResolution(string key)
        {
            return Resolutions.Any(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        private ResolutionOption ResolveResolution(string key)
        {
            key = NormalizeResolutionKey(key);
            if (key == "auto4x3")
            {
                return BuildAutoResolution();
            }
            if (key == "current")
            {
                return ReadCurrentResolution();
            }

            foreach (ResolutionOption option in Resolutions)
            {
                if (option.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            throw new InvalidOperationException("지원하지 않는 해상도입니다: " + key);
        }

        private ResolutionOption ResolveApplyResolution(LauncherSettings settings)
        {
            if (NormalizeDisplayMode(settings.DisplayMode) == "original_fullscreen")
            {
                return new ResolutionOption("640x480", "원본 640 x 480", 640, 480);
            }

            return ResolveResolution(settings.Resolution);
        }

        private ResolutionOption ReadCurrentResolution()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistrySubKey))
            {
                if (key != null)
                {
                    int width = ReadInt(key, "ScreenWidth", 640);
                    int height = ReadInt(key, "ScreenHeight", 480);
                    if (width >= 320 && height >= 240)
                    {
                        return new ResolutionOption("current", "게임 설정값 사용", width, height);
                    }
                }
            }
            return new ResolutionOption("current", "게임 설정값 사용", 640, 480);
        }

        private void WriteGameRegistry(LauncherSettings settings, ResolutionOption resolution)
        {
            int fullScreen = 1;
            string displayMode = NormalizeDisplayMode(settings.DisplayMode);
            if (displayMode == "windowed" || displayMode == "borderless_window")
            {
                fullScreen = 0;
            }
            else if (displayMode == "current")
            {
                using (RegistryKey existing = Registry.CurrentUser.OpenSubKey(RegistrySubKey))
                {
                    fullScreen = existing != null && ReadInt(existing, "FullScreen", 1) == 0 ? 0 : 1;
                }
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistrySubKey))
            {
                key.SetValue("SafetyComplete", 1, RegistryValueKind.DWord);
                key.SetValue("FullScreen", fullScreen, RegistryValueKind.DWord);
                key.SetValue("ScreenWidth", resolution.Width, RegistryValueKind.DWord);
                key.SetValue("ScreenHeight", resolution.Height, RegistryValueKind.DWord);
                key.SetValue("ScreenBpp", settings.Bpp, RegistryValueKind.DWord);
                key.SetValue("AlphaTest", 1, RegistryValueKind.DWord);
                key.SetValue("AlphaGouraud", 1, RegistryValueKind.DWord);
            }
        }

        private void StartBorderlessHelper(int processId, LauncherSettings settings)
        {
            ResolutionOption resolution = ResolveResolution(settings.Resolution);
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Application.ExecutablePath;
            startInfo.WorkingDirectory = gameDir;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.Arguments = string.Format("/borderless-helper -Pid {0} -Width {1} -Height {2}", processId, resolution.Width, resolution.Height);
            Process.Start(startInfo);
        }

        private void ApplyBorderlessWindow(Process process, int width, int height)
        {
            IntPtr hwnd = WaitForGameWindow(process, 10000);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            bool wroteTrace = false;
            while (!process.HasExited)
            {
                IntPtr refreshed = FindTopLevelWindow(process.Id);
                if (refreshed != IntPtr.Zero)
                {
                    hwnd = refreshed;
                }

                Rectangle bounds = Screen.FromHandle(hwnd).Bounds;
                Size target = FitInside(width, height, bounds.Width, bounds.Height);
                int x = bounds.Left + (bounds.Width - target.Width) / 2;
                int y = bounds.Top + (bounds.Height - target.Height) / 2;
                bool active = IsForegroundProcess(process.Id);

                MakeBorderless(hwnd, x, y, target.Width, target.Height, active);
                if (!wroteTrace)
                {
                    WriteBorderlessHelperTrace(hwnd, x, y, target.Width, target.Height, active);
                    wroteTrace = true;
                }

                Thread.Sleep(stopwatch.ElapsedMilliseconds < 15000 ? 250 : 750);
            }
        }

        private IntPtr WaitForGameWindow(Process process, int timeoutMs)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (process.HasExited)
                {
                    return IntPtr.Zero;
                }

                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }

                IntPtr found = FindTopLevelWindow(process.Id);
                if (found != IntPtr.Zero)
                {
                    return found;
                }

                Thread.Sleep(100);
            }

            return IntPtr.Zero;
        }

        private IntPtr FindTopLevelWindow(int processId)
        {
            IntPtr result = IntPtr.Zero;
            NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
            {
                int windowProcessId;
                NativeMethods.GetWindowThreadProcessId(hwnd, out windowProcessId);
                if (windowProcessId == processId && NativeMethods.IsWindowVisible(hwnd))
                {
                    result = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        private bool IsForegroundProcess(int processId)
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                return false;
            }

            int foregroundProcessId;
            NativeMethods.GetWindowThreadProcessId(foreground, out foregroundProcessId);
            return foregroundProcessId == processId;
        }

        private void MakeBorderless(IntPtr hwnd, int x, int y, int width, int height, bool active)
        {
            int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlStyle);
            style &= ~(NativeMethods.WsCaption | NativeMethods.WsThickFrame | NativeMethods.WsMinimizeBox | NativeMethods.WsMaximizeBox | NativeMethods.WsSysMenu | NativeMethods.WsBorder | NativeMethods.WsDlgFrame);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GwlStyle, style);

            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlExStyle);
            exStyle &= ~(NativeMethods.WsExDlgModalFrame | NativeMethods.WsExWindowEdge | NativeMethods.WsExClientEdge | NativeMethods.WsExStaticEdge);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GwlExStyle, exStyle);

            NativeMethods.ShowWindow(hwnd, NativeMethods.SwShow);
            NativeMethods.SetWindowPos(hwnd, active ? NativeMethods.HwndTopmost : NativeMethods.HwndNotopmost, x, y, width, height, NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged | NativeMethods.SwpShowWindow);
        }

        private void WriteBorderlessHelperTrace(IntPtr hwnd, int x, int y, int width, int height, bool active)
        {
            try
            {
                int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlStyle);
                int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlExStyle);
                string text = string.Format(
                    "time={0:yyyy-MM-dd HH:mm:ss}\r\nhwnd=0x{1:X}\r\nrect={2},{3},{4},{5}\r\nactive={6}\r\nstyle=0x{7:X8}\r\nexstyle=0x{8:X8}\r\n",
                    DateTime.Now,
                    hwnd.ToInt64(),
                    x,
                    y,
                    width,
                    height,
                    active,
                    style,
                    exStyle);
                Directory.CreateDirectory(AnalysisDir);
                File.WriteAllText(Path.Combine(AnalysisDir, "FinalTacticsLauncher.borderless-helper.last.txt"), text, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private Size FitInside(int width, int height, int maxWidth, int maxHeight)
        {
            if (width <= maxWidth && height <= maxHeight)
            {
                return new Size(width, height);
            }

            double scale = Math.Min(maxWidth / (double)width, maxHeight / (double)height);
            return new Size(Math.Max(640, (int)Math.Floor(width * scale)), Math.Max(480, (int)Math.Floor(height * scale)));
        }

        private void WriteDDrawCompatConfig(string profile, string fpsLimit, string dpiMode)
        {
            List<string> lines = new List<string>();
            string fpsSetting = fpsLimit == "off" ? "FpsLimiter = off" : "FpsLimiter = msgloop(" + fpsLimit + ")";
            string normalizedDpi = NormalizeDpiMode(dpiMode);
            string dpiSetting = "DpiAwareness = " + normalizedDpi;

            if (profile == "baseline" && fpsLimit == "off" && normalizedDpi == "permonitor")
            {
                if (File.Exists(DDrawCompatIni))
                {
                    File.Delete(DDrawCompatIni);
                }
                return;
            }

            lines.Add("; Generated by FinalTacticsLauncher.exe");
            lines.Add("; Process-specific DDrawCompat settings for FT.exe.");
            lines.Add("LogLevel = info");
            lines.Add("DisplayResolution = desktop");
            lines.Add("SupportedResolutions = native, 640x480, 800x600, 960x720, 1024x768, 1152x864, 1280x960, 1400x1050, 1440x1080, 1600x1200, 1856x1392, 1920x1440, 2048x1536, 2560x1920, 2880x2160");
            lines.Add("DisplayAspectRatio = app");
            lines.Add("RenderColorDepth = 32");
            lines.Add("VSync = app");
            lines.Add(dpiSetting);
            lines.Add(fpsSetting);

            if (profile == "bicubic")
            {
                lines.Add("FullscreenMode = borderless");
                lines.Add("DisplayFilter = bicubic(25)");
                lines.Add("ResolutionScale = app(1)");
                lines.Add("ResolutionScaleFilter = point");
            }
            else if (profile == "lanczos")
            {
                lines.Add("FullscreenMode = borderless");
                lines.Add("DisplayFilter = lanczos(3)");
                lines.Add("ResolutionScale = app(1)");
                lines.Add("ResolutionScaleFilter = point");
            }
            else if (profile == "integer")
            {
                lines.Add("FullscreenMode = borderless");
                lines.Add("DisplayFilter = integer");
                lines.Add("ResolutionScale = app(1)");
                lines.Add("ResolutionScaleFilter = point");
            }
            else if (profile == "render_upscale" || profile == "render_upscale_msaa4x")
            {
                lines.Add("FullscreenMode = borderless");
                lines.Add("DisplayFilter = bicubic(25)");
                lines.Add("ResolutionScale = display(1)");
                lines.Add("ResolutionScaleFilter = point");
                lines.Add(profile == "render_upscale_msaa4x" ? "Antialiasing = msaa4x(0)" : "Antialiasing = off");
                lines.Add("SpriteDetection = zconst");
                lines.Add("SpriteFilter = point");
                lines.Add("SpriteTexCoord = clamp");
            }

            File.WriteAllLines(DDrawCompatIni, lines.ToArray(), Encoding.ASCII);
        }

        private void BackupDDrawCompatConfig(string profile)
        {
            if (!File.Exists(DDrawCompatIni))
            {
                return;
            }

            Directory.CreateDirectory(AnalysisDir);
            string path = Path.Combine(AnalysisDir, string.Format("DDrawCompat-FT.before_{0}_{1:yyyyMMdd_HHmmss}.ini", profile, DateTime.Now));
            File.Copy(DDrawCompatIni, path, true);
        }

        private void RestoreGeneratedDDrawCompatConfig(List<string> actions, List<string> notes)
        {
            if (!File.Exists(DDrawCompatIni))
            {
                return;
            }

            if (!IsGeneratedDDrawCompatConfig())
            {
                notes.Add("DDrawCompat-FT.ini는 런처 생성 파일로 확인되지 않아 보존했습니다.");
                return;
            }

            BackupFile(DDrawCompatIni, "DDrawCompat-FT.before_original_restore");
            File.Delete(DDrawCompatIni);
            actions.Add("런처가 생성한 DDrawCompat-FT.ini를 제거했습니다.");
        }

        private bool IsGeneratedDDrawCompatConfig()
        {
            try
            {
                foreach (string line in File.ReadLines(DDrawCompatIni, Encoding.ASCII).Take(8))
                {
                    if (line.IndexOf("Generated by FinalTacticsLauncher.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private void RestoreSupportFile(string name, string source, string resourceName, List<string> actions, List<string> notes)
        {
            string destination = Path.Combine(gameDir, name);
            if (!File.Exists(destination))
            {
                return;
            }

            string sourceFull = Path.GetFullPath(source);
            string destinationFull = Path.GetFullPath(destination);
            if (sourceFull.Equals(destinationFull, StringComparison.OrdinalIgnoreCase))
            {
                notes.Add(name + "은 런처 폴더 자체의 파일이라 보존했습니다.");
                return;
            }

            if (ShouldPreserveExistingSupportFile(destination))
            {
                notes.Add(name + "은 기존 호환성 파일로 보여 보존했습니다.");
                return;
            }

            string backup = FindLatestSupportBackup(name);
            if (backup != null)
            {
                BackupFile(destination, name + ".before_original_restore");
                File.Copy(backup, destination, true);
                actions.Add(name + "을 적용 전 백업으로 복구했습니다.");
                return;
            }

            if (SupportFileEqualsSource(source, resourceName, destination))
            {
                BackupFile(destination, name + ".removed_by_original_restore");
                File.Delete(destination);
                actions.Add(name + " 런처 복사본을 제거했습니다.");
                return;
            }

            notes.Add(name + "은 런처 복사본으로 확인되지 않아 보존했습니다.");
        }

        private string FindLatestSupportBackup(string name)
        {
            if (!Directory.Exists(AnalysisDir))
            {
                return null;
            }

            string extension = Path.GetExtension(name);
            string pattern = name + ".before_launcher_*" + extension;
            FileInfo latest = Directory.GetFiles(AnalysisDir, pattern)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
            return latest == null ? null : latest.FullName;
        }

        private void BackupRegistry()
        {
            Directory.CreateDirectory(AnalysisDir);
            string path = Path.Combine(AnalysisDir, string.Format("BAROQUE_FinalTactics_System_before_launcher_{0:yyyyMMdd_HHmmss}.reg", DateTime.Now));
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "reg.exe";
            startInfo.Arguments = "export HKCU\\Software\\BAROQUE\\FinalTactics\\System \"" + path + "\" /y";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            Process process = Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit(3000);
            }
        }

        private void BackupFile(string path, string prefix)
        {
            Directory.CreateDirectory(AnalysisDir);
            string extension = Path.GetExtension(path);
            string backupName = string.Format("{0}_{1:yyyyMMdd_HHmmss}{2}", prefix, DateTime.Now, extension);
            File.Copy(path, Path.Combine(AnalysisDir, backupName), true);
        }

        private static bool BytesMatch(byte[] bytes, int offset, byte[] expected)
        {
            if (offset < 0 || offset + expected.Length > bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (bytes[offset + i] != expected[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static void ClearRange(byte[] bytes, int offset, int length)
        {
            if (offset < 0 || offset + length > bytes.Length)
            {
                return;
            }

            for (int i = 0; i < length; i++)
            {
                bytes[offset + i] = 0;
            }
        }

        private static bool IsZeroRange(byte[] bytes, int offset, int length)
        {
            if (offset < 0 || offset + length > bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < length; i++)
            {
                if (bytes[offset + i] != 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool FilesEqual(string left, string right)
        {
            FileInfo leftInfo = new FileInfo(left);
            FileInfo rightInfo = new FileInfo(right);
            if (leftInfo.Length != rightInfo.Length)
            {
                return false;
            }

            byte[] leftBytes = File.ReadAllBytes(left);
            byte[] rightBytes = File.ReadAllBytes(right);
            if (leftBytes.Length != rightBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < leftBytes.Length; i++)
            {
                if (leftBytes[i] != rightBytes[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool FileBytesEqual(string path, byte[] expected)
        {
            FileInfo info = new FileInfo(path);
            if (info.Length != expected.Length)
            {
                return false;
            }

            byte[] actual = File.ReadAllBytes(path);
            if (actual.Length != expected.Length)
            {
                return false;
            }

            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i] != expected[i])
                {
                    return false;
                }
            }
            return true;
        }

        private string ReadFpsLimiter()
        {
            if (!File.Exists(DDrawCompatIni))
            {
                return "기본값";
            }

            foreach (string line in File.ReadAllLines(DDrawCompatIni, Encoding.ASCII))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("FpsLimiter", StringComparison.OrdinalIgnoreCase))
                {
                    int pos = trimmed.IndexOf('=');
                    if (pos < 0)
                    {
                        return "미지정";
                    }

                    string value = trimmed.Substring(pos + 1).Trim();
                    if (value.Equals("off", StringComparison.OrdinalIgnoreCase))
                    {
                        return "제한 없음";
                    }
                    if (value.StartsWith("msgloop(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(")", StringComparison.Ordinal))
                    {
                        return value.Substring(8, value.Length - 9) + " FPS";
                    }
                    return value;
                }
            }
            return "미지정";
        }

        private string ReadDpiAwareness()
        {
            if (!File.Exists(DDrawCompatIni))
            {
                return "고DPI";
            }

            foreach (string line in File.ReadAllLines(DDrawCompatIni, Encoding.ASCII))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("DpiAwareness", StringComparison.OrdinalIgnoreCase))
                {
                    int pos = trimmed.IndexOf('=');
                    if (pos < 0)
                    {
                        return "미지정";
                    }

                    return FormatDpiMode(trimmed.Substring(pos + 1).Trim());
                }
            }
            return "고DPI";
        }

        private string FormatDpiMode(string value)
        {
            string normalized = NormalizeDpiMode(value);
            if (normalized == "unaware")
            {
                return "Windows 확대";
            }
            if (normalized == "system")
            {
                return "시스템";
            }
            if (normalized == "app")
            {
                return "앱 기본";
            }
            return "고DPI";
        }

        private List<int> GetGameProcessIds()
        {
            List<int> result = new List<int>();
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE Name='FT.exe'"))
            {
                foreach (ManagementObject item in searcher.Get())
                {
                    string path = item["ExecutablePath"] as string;
                    if (path != null && path.Equals(FtExe, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(Convert.ToInt32(item["ProcessId"]));
                    }
                }
            }
            return result;
        }

        private List<int> GetBorderlessHelperProcessIds()
        {
            List<int> result = new List<int>();
            int currentProcessId = Process.GetCurrentProcess().Id;
            string launcherExe = Application.ExecutablePath;
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process WHERE Name='FinalTacticsLauncher.exe'"))
            {
                foreach (ManagementObject item in searcher.Get())
                {
                    string path = item["ExecutablePath"] as string;
                    string commandLine = item["CommandLine"] as string;
                    if (path != null &&
                        commandLine != null &&
                        path.Equals(launcherExe, StringComparison.OrdinalIgnoreCase) &&
                        commandLine.IndexOf("borderless-helper", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int processId = Convert.ToInt32(item["ProcessId"]);
                        if (processId != currentProcessId)
                        {
                            result.Add(processId);
                        }
                    }
                }
            }
            return result;
        }

        private List<ResolutionOption> BuildResolutionOptions()
        {
            List<ResolutionOption> options = new List<ResolutionOption>();
            ResolutionOption auto = BuildAutoResolution();
            options.Add(new ResolutionOption("auto4x3", string.Format("자동 선택 ({0} x {1})", auto.Width, auto.Height), -1, -1));
            options.Add(new ResolutionOption("current", "현재 게임 설정 유지", 0, 0));

            Dictionary<string, ResolutionOption> candidates = new Dictionary<string, ResolutionOption>(StringComparer.OrdinalIgnoreCase);
            foreach (DisplayModeInfo mode in GetPrimaryDisplayModes())
            {
                AddResolutionCandidate(candidates, mode.Width, mode.Height, auto);
            }

            int[,] common =
            {
                { 640, 480 }, { 800, 600 }, { 960, 720 }, { 1024, 768 }, { 1152, 864 },
                { 1280, 960 }, { 1400, 1050 }, { 1440, 1080 }, { 1600, 1200 },
                { 1856, 1392 }, { 1920, 1440 }, { 2048, 1536 }, { 2560, 1920 },
                { 2880, 2160 }, { 3200, 2400 }, { 3840, 2880 }
            };

            for (int i = 0; i < common.GetLength(0); i++)
            {
                AddResolutionCandidate(candidates, common[i, 0], common[i, 1], auto);
            }
            AddResolutionCandidate(candidates, auto.Width, auto.Height, auto);

            foreach (ResolutionOption option in candidates.Values.OrderBy(r => r.Width * r.Height).ThenBy(r => r.Width))
            {
                options.Add(option);
            }

            return options;
        }

        private void AddResolutionCandidate(Dictionary<string, ResolutionOption> candidates, int width, int height, ResolutionOption max)
        {
            if (width < 640 || height < 480 || width > max.Width || height > max.Height)
            {
                return;
            }
            if (width * 3 != height * 4)
            {
                return;
            }

            string key = string.Format("{0}x{1}", width, height);
            if (!candidates.ContainsKey(key))
            {
                candidates[key] = new ResolutionOption(key, string.Format("{0} x {1}", width, height), width, height);
            }
        }

        private ResolutionOption BuildAutoResolution()
        {
            DesktopSize desktop = GetPrimaryDesktopSize();
            int width;
            int height;
            if (desktop.Height * 4 <= desktop.Width * 3)
            {
                height = (desktop.Height / 3) * 3;
                width = (height / 3) * 4;
            }
            else
            {
                width = (desktop.Width / 4) * 4;
                height = (width / 4) * 3;
            }

            if (width < 640 || height < 480)
            {
                width = 640;
                height = 480;
            }

            return new ResolutionOption("auto4x3", "자동 선택", width, height);
        }

        private DesktopSize GetPrimaryDesktopSize()
        {
            DevMode mode = new DevMode();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DevMode));
            if (NativeMethods.EnumDisplaySettings(null, NativeMethods.EnumCurrentSettings, ref mode) && mode.dmPelsWidth >= 640 && mode.dmPelsHeight >= 480)
            {
                return new DesktopSize(mode.dmPelsWidth, mode.dmPelsHeight);
            }

            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            return new DesktopSize(bounds.Width, bounds.Height);
        }

        private List<DisplayModeInfo> GetPrimaryDisplayModes()
        {
            List<DisplayModeInfo> result = new List<DisplayModeInfo>();
            for (int index = 0; ; index++)
            {
                DevMode mode = new DevMode();
                mode.dmSize = (short)Marshal.SizeOf(typeof(DevMode));
                if (!NativeMethods.EnumDisplaySettings(null, index, ref mode))
                {
                    break;
                }

                if (mode.dmPelsWidth >= 640 && mode.dmPelsHeight >= 480)
                {
                    result.Add(new DisplayModeInfo(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmBitsPerPel, mode.dmDisplayFrequency));
                }
            }
            return result;
        }

        private static int ReadInt(RegistryKey key, string name, int fallback)
        {
            object value = key.GetValue(name);
            if (value == null)
            {
                return fallback;
            }
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }
    }

    internal sealed class LauncherSettings
    {
        public string GameDir;
        public string Profile;
        public string Resolution;
        public int Bpp;
        public string DisplayMode;
        public string FpsLimit;
        public string DpiMode;
        public bool ApplyEnginePatches;
        public bool PrepareStartupAssets;

        public static LauncherSettings Default()
        {
            return new LauncherSettings
            {
                GameDir = string.Empty,
                Profile = "render_upscale",
                Resolution = "auto4x3",
                Bpp = 32,
                DisplayMode = "fullscreen",
                FpsLimit = "30",
                DpiMode = "permonitor",
                ApplyEnginePatches = true,
                PrepareStartupAssets = true
            };
        }
    }

    internal sealed class ResolutionOption
    {
        public ResolutionOption(string key, string label, int width, int height)
        {
            Key = key;
            Label = label;
            Width = width;
            Height = height;
        }

        public string Key;
        public string Label;
        public int Width;
        public int Height;

        public bool IsConcrete
        {
            get { return Width > 0 && Height > 0; }
        }
    }

    internal sealed class DesktopSize
    {
        public DesktopSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width;
        public int Height;
    }

    internal sealed class DisplayModeInfo
    {
        public DisplayModeInfo(int width, int height, int bpp, int refreshRate)
        {
            Width = width;
            Height = height;
            Bpp = bpp;
            RefreshRate = refreshRate;
        }

        public int Width;
        public int Height;
        public int Bpp;
        public int RefreshRate;
    }

    internal sealed class BytePatch
    {
        public BytePatch(string variantName, int offset, byte[] original, byte[] patched)
        {
            VariantName = variantName;
            Offset = offset;
            Original = original;
            Patched = patched;
        }

        public string VariantName;
        public int Offset;
        public byte[] Original;
        public byte[] Patched;
    }

    internal sealed class InputLatchPatch
    {
        public InputLatchPatch(string variantName, int entryOffset, int caveOffset, byte[] original, byte[] patchedEntry, byte[] hook)
        {
            VariantName = variantName;
            EntryOffset = entryOffset;
            CaveOffset = caveOffset;
            Original = original;
            PatchedEntry = patchedEntry;
            Hook = hook;
        }

        public string VariantName;
        public int EntryOffset;
        public int CaveOffset;
        public byte[] Original;
        public byte[] PatchedEntry;
        public byte[] Hook;
    }

    internal static class CommandLine
    {
        public static bool Has(string[] args, string name)
        {
            foreach (string arg in args)
            {
                if (arg.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static void ApplyOverrides(string[] args, LauncherSettings settings)
        {
            string value;
            if (TryGet(args, "-Profile", out value)) settings.Profile = value;
            if (TryGet(args, "-Resolution", out value)) settings.Resolution = value;
            if (TryGet(args, "-DisplayMode", out value)) settings.DisplayMode = value;
            if (TryGet(args, "-FpsLimit", out value)) settings.FpsLimit = value;
            if (TryGet(args, "-DpiMode", out value)) settings.DpiMode = value;
            if (TryGet(args, "-GameDir", out value)) settings.GameDir = value;
            if (TryGet(args, "-StartupAssets", out value)) settings.PrepareStartupAssets = ParseBool(value, settings.PrepareStartupAssets);
            if (TryGet(args, "-EnginePatches", out value)) settings.ApplyEnginePatches = ParseBool(value, settings.ApplyEnginePatches);
            if (TryGet(args, "-Bpp", out value))
            {
                int bpp;
                if (int.TryParse(value, out bpp) && (bpp == 16 || bpp == 32))
                {
                    settings.Bpp = bpp;
                }
            }
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "1" || normalized == "true" || normalized == "yes" || normalized == "on")
            {
                return true;
            }
            if (normalized == "0" || normalized == "false" || normalized == "no" || normalized == "off")
            {
                return false;
            }
            return fallback;
        }

        public static bool TryGet(string[] args, string name, out string value)
        {
            value = null;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    value = args[i + 1];
                    return true;
                }
                if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(name.Length + 1);
                    return true;
                }
            }
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
    }

    internal static class NativeMethods
    {
        public const int EnumCurrentSettings = -1;
        public const int GwlStyle = -16;
        public const int GwlExStyle = -20;
        public const int SwShow = 5;
        public const int SwpNoActivate = 0x0010;
        public const int SwpFrameChanged = 0x0020;
        public const int SwpShowWindow = 0x0040;
        public const int WsBorder = 0x00800000;
        public const int WsCaption = 0x00C00000;
        public const int WsDlgFrame = 0x00400000;
        public const int WsMaximizeBox = 0x00010000;
        public const int WsMinimizeBox = 0x00020000;
        public const int WsSysMenu = 0x00080000;
        public const int WsThickFrame = 0x00040000;
        public const int WsExClientEdge = 0x00000200;
        public const int WsExDlgModalFrame = 0x00000001;
        public const int WsExStaticEdge = 0x00020000;
        public const int WsExWindowEdge = 0x00000100;
        public static readonly IntPtr HwndTopmost = new IntPtr(-1);
        public static readonly IntPtr HwndNotopmost = new IntPtr(-2);

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        public static void EnableDpiAwarenessForHelper()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    return;
                }
                if (SetProcessDpiAwarenessContext(new IntPtr(-3)))
                {
                    return;
                }
            }
            catch
            {
            }

            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(int processId);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newLong);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, int flags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hwnd, int command);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hwnd);
    }
}
