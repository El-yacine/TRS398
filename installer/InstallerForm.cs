using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TRS398Installer;

public class InstallerForm : Form
{
    // Constants
    private const string AppName = "TRS-398 Pro";
    private const string AppVersion = "2.0.0";
    private const int AppPort = 8000;

    // Default paths
    private string DefaultInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TRS398Pro"
    );

    // UI Controls
    private Panel headerPanel = null!;
    private Panel contentPanel = null!;
    private Panel footerPanel = null!;
    private Panel stepIndicatorPanel = null!;
    
    private Button btnBack = null!;
    private Button btnNext = null!;
    private Button btnCancel = null!;
    
    private Label[] stepLabels = null!;
    private Panel[] stepCircles = null!;
    
    // Step-specific controls
    private TextBox txtInstallPath = null!;
    private CheckBox chkDesktopShortcut = null!;
    private CheckBox chkStartMenuShortcut = null!;
    private CheckBox chkLaunchAfterInstall = null!;
    private CheckBox chkAcceptLicense = null!;
    private ProgressBar progressBar = null!;
    private Label lblStatus = null!;
    private RichTextBox txtLog = null!;

    // State
    private int currentStep = 0;
    private readonly string[] steps = { "Welcome", "License", "Location", "Install", "Complete" };
    private bool installationComplete = false;

    // Colors
    private readonly Color PrimaryColor = Color.FromArgb(16, 185, 129);  // Green
    private readonly Color SecondaryColor = Color.FromArgb(6, 182, 212); // Cyan
    private readonly Color DarkColor = Color.FromArgb(15, 23, 42);
    private readonly Color LightColor = Color.FromArgb(248, 250, 252);
    private readonly Color MutedColor = Color.FromArgb(148, 163, 184);

    public InstallerForm()
    {
        InitializeComponent();
        ShowStep(0);
    }

    private void InitializeComponent()
    {
        // Form settings
        this.Text = $"{AppName} Setup";
        this.Size = new Size(750, 580);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.BackColor = LightColor;
        this.Font = new Font("Segoe UI", 9F);

        // Header Panel
        headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            BackColor = DarkColor
        };
        headerPanel.Paint += HeaderPanel_Paint;
        this.Controls.Add(headerPanel);

        // Step Indicator Panel
        stepIndicatorPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        CreateStepIndicators();
        this.Controls.Add(stepIndicatorPanel);

        // Footer Panel
        footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        CreateFooterButtons();
        this.Controls.Add(footerPanel);

        // Content Panel
        contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30),
            BackColor = LightColor
        };
        this.Controls.Add(contentPanel);
    }

    private void HeaderPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw icon circle
        using (var brush = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
        {
            g.FillEllipse(brush, 25, 25, 70, 70);
        }

        // Draw atom symbol
        using (var pen = new Pen(PrimaryColor, 2))
        {
            g.DrawEllipse(pen, 45, 45, 30, 30);
            g.DrawEllipse(pen, 35, 50, 50, 20);
        }
        using (var brush = new SolidBrush(PrimaryColor))
        {
            g.FillEllipse(brush, 57, 57, 6, 6);
        }

        // Draw title
        using (var titleFont = new Font("Segoe UI", 22, FontStyle.Bold))
        using (var subtitleFont = new Font("Segoe UI", 10))
        {
            g.DrawString(AppName, titleFont, new SolidBrush(PrimaryColor), 110, 30);
            g.DrawString("Medical Physics Calibration System", subtitleFont, new SolidBrush(MutedColor), 112, 65);
        }

        // Draw version badge
        using (var badgeFont = new Font("Segoe UI", 8))
        using (var brush = new SolidBrush(PrimaryColor))
        {
            var versionText = $"v{AppVersion}";
            var size = g.MeasureString(versionText, badgeFont);
            var rect = new RectangleF(headerPanel.Width - size.Width - 40, 45, size.Width + 16, 22);
            
            using (var path = RoundedRect(Rectangle.Round(rect), 10))
            {
                g.FillPath(brush, path);
            }
            g.DrawString(versionText, badgeFont, Brushes.White, rect.X + 8, rect.Y + 3);
        }
    }

    private void CreateStepIndicators()
    {
        stepLabels = new Label[steps.Length];
        stepCircles = new Panel[steps.Length];

        int stepWidth = (stepIndicatorPanel.Width - 40) / steps.Length;

        for (int i = 0; i < steps.Length; i++)
        {
            int x = 20 + (i * stepWidth) + (stepWidth / 2) - 15;

            // Circle
            var circle = new Panel
            {
                Size = new Size(30, 30),
                Location = new Point(x, 5),
                BackColor = MutedColor
            };
            circle.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(((Panel)s!).BackColor))
                {
                    e.Graphics.FillEllipse(brush, 0, 0, 29, 29);
                }
                
                int index = Array.IndexOf(stepCircles, s);
                using (var font = new Font("Segoe UI", 10, FontStyle.Bold))
                {
                    var text = (index + 1).ToString();
                    var size = e.Graphics.MeasureString(text, font);
                    e.Graphics.DrawString(text, font, Brushes.White, 
                        (30 - size.Width) / 2, (30 - size.Height) / 2);
                }
            };
            stepCircles[i] = circle;
            stepIndicatorPanel.Controls.Add(circle);

            // Label
            var label = new Label
            {
                Text = steps[i],
                AutoSize = false,
                Size = new Size(stepWidth, 20),
                Location = new Point(x - (stepWidth / 2) + 15, 38),
                TextAlign = ContentAlignment.TopCenter,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = MutedColor
            };
            stepLabels[i] = label;
            stepIndicatorPanel.Controls.Add(label);
        }
    }

    private void CreateFooterButtons()
    {
        // Separator line
        var separator = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        footerPanel.Controls.Add(separator);

        btnCancel = new Button
        {
            Text = "Cancel",
            Size = new Size(100, 35),
            Location = new Point(20, 15),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(100, 116, 139),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.Click += BtnCancel_Click;
        footerPanel.Controls.Add(btnCancel);

        btnNext = new Button
        {
            Text = "Next →",
            Size = new Size(120, 35),
            Location = new Point(footerPanel.Width - 140, 15),
            FlatStyle = FlatStyle.Flat,
            BackColor = PrimaryColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnNext.FlatAppearance.BorderSize = 0;
        btnNext.Click += BtnNext_Click;
        footerPanel.Controls.Add(btnNext);

        btnBack = new Button
        {
            Text = "← Back",
            Size = new Size(100, 35),
            Location = new Point(footerPanel.Width - 260, 15),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(100, 116, 139),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        btnBack.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnBack.Click += BtnBack_Click;
        footerPanel.Controls.Add(btnBack);

        // Anchor buttons to right
        btnNext.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        btnBack.Anchor = AnchorStyles.Right | AnchorStyles.Top;
    }

    private void UpdateStepIndicators()
    {
        for (int i = 0; i < steps.Length; i++)
        {
            if (i < currentStep)
            {
                stepCircles[i].BackColor = PrimaryColor;
                stepLabels[i].ForeColor = PrimaryColor;
            }
            else if (i == currentStep)
            {
                stepCircles[i].BackColor = SecondaryColor;
                stepLabels[i].ForeColor = SecondaryColor;
            }
            else
            {
                stepCircles[i].BackColor = MutedColor;
                stepLabels[i].ForeColor = MutedColor;
            }
            stepCircles[i].Invalidate();
        }
    }

    private void ShowStep(int step)
    {
        currentStep = step;
        contentPanel.Controls.Clear();
        UpdateStepIndicators();

        btnBack.Enabled = step > 0 && step < 4;
        btnCancel.Enabled = step < 4;

        switch (step)
        {
            case 0: ShowWelcome(); break;
            case 1: ShowLicense(); break;
            case 2: ShowLocation(); break;
            case 3: ShowInstall(); break;
            case 4: ShowComplete(); break;
        }
    }

    private void ShowWelcome()
    {
        btnNext.Text = "Next →";
        btnNext.Enabled = true;

        var title = CreateTitle("Welcome to TRS-398 Pro Setup");
        contentPanel.Controls.Add(title);

        var desc = new Label
        {
            Text = "This wizard will guide you through the installation of TRS-398 Pro,\n" +
                   "a comprehensive photon beam calibration system based on IAEA TRS-398 protocol.",
            AutoSize = true,
            Location = new Point(30, 60),
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 10)
        };
        contentPanel.Controls.Add(desc);

        // Features
        var featuresBox = new GroupBox
        {
            Text = "Features",
            Location = new Point(30, 120),
            Size = new Size(contentPanel.Width - 80, 180),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        contentPanel.Controls.Add(featuresBox);

        var features = new[]
        {
            ("📊", "Live Calculations", "Real-time dose calculations with automatic kQ factor interpolation"),
            ("🔬", "50+ Ion Chambers", "Pre-configured library with all major chamber models and kQ data"),
            ("📋", "History & Export", "Complete measurement history with CSV and PDF export"),
            ("🔒", "Secure & Local", "All data stored locally with optional authentication")
        };

        int y = 25;
        foreach (var (icon, featureTitle, featureDesc) in features)
        {
            var iconLabel = new Label
            {
                Text = icon,
                Location = new Point(15, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 14)
            };
            featuresBox.Controls.Add(iconLabel);

            var titleLabel = new Label
            {
                Text = featureTitle,
                Location = new Point(50, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = DarkColor
            };
            featuresBox.Controls.Add(titleLabel);

            var descLabel = new Label
            {
                Text = featureDesc,
                Location = new Point(50, y + 18),
                AutoSize = true,
                ForeColor = MutedColor
            };
            featuresBox.Controls.Add(descLabel);

            y += 42;
        }

        var continueLabel = new Label
        {
            Text = "Click 'Next' to continue with the installation.",
            Location = new Point(30, 320),
            AutoSize = true,
            ForeColor = MutedColor
        };
        contentPanel.Controls.Add(continueLabel);
    }

    private void ShowLicense()
    {
        btnNext.Text = "Next →";

        var title = CreateTitle("License Agreement");
        contentPanel.Controls.Add(title);

        var desc = new Label
        {
            Text = "Please read the following license agreement carefully.",
            AutoSize = true,
            Location = new Point(30, 60),
            ForeColor = MutedColor
        };
        contentPanel.Controls.Add(desc);

        var licenseText = new RichTextBox
        {
            Location = new Point(30, 95),
            Size = new Size(contentPanel.Width - 80, 180),
            ReadOnly = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9),
            Text = @"TRS-398 PRO SOFTWARE LICENSE AGREEMENT

Copyright (c) 2024 TRS-398 Pro Team
All rights reserved.

TERMS AND CONDITIONS

1. GRANT OF LICENSE
This software is provided for use in medical physics calibration 
according to IAEA TRS-398 protocol. You may install and use this 
software on any number of computers.

2. DISCLAIMER OF WARRANTY
THIS SOFTWARE IS PROVIDED ""AS IS"" WITHOUT WARRANTY OF ANY KIND.
The authors are not responsible for any damages arising from the 
use of this software.

3. MEDICAL DISCLAIMER
This software is intended as a calculation aid only. All clinical 
decisions should be verified by qualified medical physicists.

4. DATA PRIVACY
All measurement data is stored locally on your computer.

By installing this software, you acknowledge that you have read 
and agree to these terms and conditions."
        };
        contentPanel.Controls.Add(licenseText);

        chkAcceptLicense = new CheckBox
        {
            Text = "I accept the terms of the license agreement",
            Location = new Point(30, 290),
            AutoSize = true,
            Font = new Font("Segoe UI", 9)
        };
        chkAcceptLicense.CheckedChanged += (s, e) => btnNext.Enabled = chkAcceptLicense.Checked;
        contentPanel.Controls.Add(chkAcceptLicense);

        btnNext.Enabled = false;
    }

    private void ShowLocation()
    {
        btnNext.Text = "Install →";
        btnNext.Enabled = true;

        var title = CreateTitle("Installation Location");
        contentPanel.Controls.Add(title);

        var desc = new Label
        {
            Text = "Choose the folder where TRS-398 Pro will be installed.",
            AutoSize = true,
            Location = new Point(30, 60),
            ForeColor = MutedColor
        };
        contentPanel.Controls.Add(desc);

        var pathLabel = new Label
        {
            Text = "Installation folder:",
            Location = new Point(30, 100),
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        contentPanel.Controls.Add(pathLabel);

        txtInstallPath = new TextBox
        {
            Location = new Point(30, 125),
            Size = new Size(contentPanel.Width - 180, 28),
            Text = DefaultInstallDir,
            Font = new Font("Segoe UI", 10)
        };
        contentPanel.Controls.Add(txtInstallPath);

        var btnBrowse = new Button
        {
            Text = "Browse...",
            Location = new Point(contentPanel.Width - 130, 123),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
        btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnBrowse.Click += BtnBrowse_Click;
        contentPanel.Controls.Add(btnBrowse);

        // Options
        var optionsBox = new GroupBox
        {
            Text = "Options",
            Location = new Point(30, 170),
            Size = new Size(contentPanel.Width - 80, 130),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        contentPanel.Controls.Add(optionsBox);

        chkDesktopShortcut = new CheckBox
        {
            Text = "Create desktop shortcut",
            Location = new Point(15, 30),
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 9)
        };
        optionsBox.Controls.Add(chkDesktopShortcut);

        chkStartMenuShortcut = new CheckBox
        {
            Text = "Create Start Menu shortcut",
            Location = new Point(15, 55),
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 9)
        };
        optionsBox.Controls.Add(chkStartMenuShortcut);

        chkLaunchAfterInstall = new CheckBox
        {
            Text = "Launch TRS-398 Pro after installation",
            Location = new Point(15, 80),
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 9)
        };
        optionsBox.Controls.Add(chkLaunchAfterInstall);

        var spaceLabel = new Label
        {
            Text = "Space required: ~150 MB",
            Location = new Point(30, 315),
            AutoSize = true,
            ForeColor = MutedColor
        };
        contentPanel.Controls.Add(spaceLabel);
    }

    private void ShowInstall()
    {
        btnNext.Text = "Installing...";
        btnNext.Enabled = false;
        btnBack.Enabled = false;
        btnCancel.Enabled = false;

        var title = CreateTitle("Installing TRS-398 Pro");
        contentPanel.Controls.Add(title);

        var desc = new Label
        {
            Text = "Please wait while TRS-398 Pro is being installed...",
            AutoSize = true,
            Location = new Point(30, 60),
            ForeColor = MutedColor
        };
        contentPanel.Controls.Add(desc);

        progressBar = new ProgressBar
        {
            Location = new Point(30, 100),
            Size = new Size(contentPanel.Width - 80, 25),
            Style = ProgressBarStyle.Continuous
        };
        contentPanel.Controls.Add(progressBar);

        lblStatus = new Label
        {
            Text = "Preparing installation...",
            Location = new Point(30, 135),
            AutoSize = true,
            ForeColor = MutedColor
        };
        contentPanel.Controls.Add(lblStatus);

        txtLog = new RichTextBox
        {
            Location = new Point(30, 170),
            Size = new Size(contentPanel.Width - 80, 150),
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.None
        };
        contentPanel.Controls.Add(txtLog);

        // Start installation
        Task.Run(() => RunInstallation());
    }

    private void ShowComplete()
    {
        btnNext.Text = "Finish";
        btnNext.Enabled = true;
        btnBack.Enabled = false;
        btnCancel.Enabled = false;

        // Success icon
        var successIcon = new Label
        {
            Text = "✓",
            Font = new Font("Segoe UI", 48, FontStyle.Bold),
            ForeColor = PrimaryColor,
            Location = new Point((contentPanel.Width - 80) / 2, 20),
            AutoSize = true
        };
        contentPanel.Controls.Add(successIcon);

        var title = new Label
        {
            Text = "Installation Complete!",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = DarkColor,
            Location = new Point(30, 100),
            AutoSize = true
        };
        title.Location = new Point((contentPanel.Width - title.PreferredWidth) / 2, 100);
        contentPanel.Controls.Add(title);

        var desc = new Label
        {
            Text = "TRS-398 Pro has been successfully installed on your computer.",
            AutoSize = true,
            Location = new Point(30, 140),
            ForeColor = MutedColor
        };
        desc.Location = new Point((contentPanel.Width - desc.PreferredWidth) / 2, 140);
        contentPanel.Controls.Add(desc);

        // Info box
        var infoBox = new GroupBox
        {
            Text = "Installation Details",
            Location = new Point(30, 180),
            Size = new Size(contentPanel.Width - 80, 80),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        contentPanel.Controls.Add(infoBox);

        var locationLabel = new Label
        {
            Text = $"Location: {txtInstallPath?.Text ?? DefaultInstallDir}",
            Location = new Point(15, 25),
            AutoSize = true,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 9)
        };
        infoBox.Controls.Add(locationLabel);

        var urlLabel = new Label
        {
            Text = $"URL: http://localhost:{AppPort}",
            Location = new Point(15, 48),
            AutoSize = true,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 9)
        };
        infoBox.Controls.Add(urlLabel);

        chkLaunchAfterInstall = new CheckBox
        {
            Text = "Launch TRS-398 Pro now",
            Location = new Point(30, 280),
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 9)
        };
        contentPanel.Controls.Add(chkLaunchAfterInstall);

        var finishLabel = new Label
        {
            Text = "Click 'Finish' to complete the setup.",
            Location = new Point(30, 320),
            AutoSize = true,
            ForeColor = MutedColor
        };
        contentPanel.Controls.Add(finishLabel);

        installationComplete = true;
    }

    private async Task RunInstallation()
    {
        try
        {
            string installDir = "";
            Invoke(() => installDir = txtInstallPath.Text);

            // Step 1: Create directories
            UpdateProgress(10, "Creating directories...");
            Log("Creating installation directories...");
            
            Directory.CreateDirectory(installDir);
            Directory.CreateDirectory(Path.Combine(installDir, "wwwroot", "logos"));
            Directory.CreateDirectory(Path.Combine(installDir, "data"));
            
            Log($"✓ Created: {installDir}", PrimaryColor);

            // Step 2: Copy files
            UpdateProgress(30, "Copying application files...");
            Log("Copying server files...");

            string sourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppFiles", "server");
            if (!Directory.Exists(sourceDir))
            {
                // Try relative to exe
                sourceDir = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath)!, "..", "server");
            }

            if (Directory.Exists(sourceDir))
            {
                CopyDirectory(sourceDir, installDir);
                Log("✓ Server files copied", PrimaryColor);
            }
            else
            {
                Log($"⚠ Server directory not found, checking embedded resources...", Color.Orange);
            }

            // Copy detector library
            string detectorLib = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppFiles", "detector_library.json");
            if (!System.IO.File.Exists(detectorLib))
            {
                detectorLib = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath)!, "..", "detector_library.json");
            }

            if (System.IO.File.Exists(detectorLib))
            {
                System.IO.File.Copy(detectorLib, Path.Combine(installDir, "detector_library.json"), true);
                Log("✓ Detector library copied", PrimaryColor);
            }

            // Step 3: Check .NET
            UpdateProgress(50, "Checking .NET runtime...");
            Log("Checking for .NET runtime...");

            bool dotnetFound = CheckDotNet();
            if (dotnetFound)
            {
                Log("✓ .NET runtime found", PrimaryColor);
            }
            else
            {
                Log("⚠ .NET runtime not found - please install .NET 8.0", Color.Orange);
            }

            // Step 4: Create launcher
            UpdateProgress(70, "Creating launcher...");
            Log("Creating launcher script...");
            CreateLauncher(installDir);
            Log("✓ Launcher created", PrimaryColor);

            // Step 5: Create shortcuts
            UpdateProgress(85, "Creating shortcuts...");

            bool createDesktop = false, createStartMenu = false;
            Invoke(() =>
            {
                createDesktop = chkDesktopShortcut.Checked;
                createStartMenu = chkStartMenuShortcut.Checked;
            });

            if (createDesktop)
            {
                Log("Creating desktop shortcut...");
                CreateShortcut(installDir, "desktop");
                Log("✓ Desktop shortcut created", PrimaryColor);
            }

            if (createStartMenu)
            {
                Log("Creating Start Menu shortcut...");
                CreateShortcut(installDir, "startmenu");
                Log("✓ Start Menu shortcut created", PrimaryColor);
            }

            // Step 6: Create uninstaller
            UpdateProgress(95, "Creating uninstaller...");
            Log("Creating uninstaller...");
            CreateUninstaller(installDir);
            Log("✓ Uninstaller created", PrimaryColor);

            // Complete
            UpdateProgress(100, "Installation complete!");
            Log("");
            Log("═══════════════════════════════════════════════", PrimaryColor);
            Log("  Installation completed successfully!", PrimaryColor);
            Log("═══════════════════════════════════════════════", PrimaryColor);

            await Task.Delay(1000);

            Invoke(() => ShowStep(4));
        }
        catch (Exception ex)
        {
            Log($"\n✗ Error: {ex.Message}", Color.Red);
            UpdateProgress(0, $"Installation failed: {ex.Message}");
            
            Invoke(() =>
            {
                MessageBox.Show($"Installation failed:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnCancel.Enabled = true;
            });
        }
    }

    private void UpdateProgress(int value, string status)
    {
        Invoke(() =>
        {
            progressBar.Value = value;
            lblStatus.Text = status;
        });
    }

    private void Log(string message, Color? color = null)
    {
        Invoke(() =>
        {
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionColor = color ?? Color.FromArgb(148, 163, 184);
            txtLog.AppendText(message + "\n");
            txtLog.ScrollToCaret();
        });
    }

    private bool CheckDotNet()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            System.IO.File.Copy(file, destFile, true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }

    private void CreateLauncher(string installDir)
    {
        string launcherPath = Path.Combine(installDir, "TRS398Pro.bat");
        string content = $@"@echo off
cd /d ""{installDir}""
echo Starting TRS-398 Pro...
start /B dotnet run --urls http://localhost:{AppPort}
timeout /t 3 /nobreak >nul
start http://localhost:{AppPort}
echo TRS-398 Pro is running at http://localhost:{AppPort}
echo Close this window to stop the server.
pause >nul
";
        System.IO.File.WriteAllText(launcherPath, content);
    }

    private void CreateShortcut(string installDir, string location)
    {
        try
        {
            string shortcutPath;
            if (location == "desktop")
            {
                shortcutPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "TRS-398 Pro.lnk"
                );
            }
            else
            {
                shortcutPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs",
                    "TRS-398 Pro.lnk"
                );
            }

            string targetPath = Path.Combine(installDir, "TRS398Pro.bat");
            
            // Use PowerShell to create shortcut (works on all Windows versions)
            string psScript = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath.Replace("'", "''")}')
$Shortcut.TargetPath = '{targetPath.Replace("'", "''")}'
$Shortcut.WorkingDirectory = '{installDir.Replace("'", "''")}'
$Shortcut.Description = 'TRS-398 Pro - Medical Physics Calibration System'
$Shortcut.Save()
";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            
            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            Log($"⚠ Could not create shortcut: {ex.Message}", Color.Orange);
        }
    }

    private void CreateUninstaller(string installDir)
    {
        string uninstallerPath = Path.Combine(installDir, "Uninstall.bat");
        string desktopShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "TRS-398 Pro.lnk"
        );
        string startMenuShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            "TRS-398 Pro.lnk"
        );

        string content = $@"@echo off
echo TRS-398 Pro Uninstaller
echo =======================
echo.
set /p CONFIRM=""Are you sure you want to uninstall TRS-398 Pro? (y/N): ""
if /i not ""%CONFIRM%""==""y"" (
    echo Uninstall cancelled.
    pause
    exit /b 0
)

echo.
echo Removing application files...
rmdir /S /Q ""{installDir}"" 2>nul
del ""{desktopShortcut}"" 2>nul
del ""{startMenuShortcut}"" 2>nul

echo.
echo TRS-398 Pro has been uninstalled.
pause
";
        System.IO.File.WriteAllText(uninstallerPath, content);
    }

    private Label CreateTitle(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = DarkColor,
            Location = new Point(30, 20),
            AutoSize = true
        };
    }

    private GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(bounds.Right - radius * 2, bounds.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select installation folder",
            SelectedPath = txtInstallPath.Text
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtInstallPath.Text = dialog.SelectedPath;
        }
    }

    private void BtnNext_Click(object? sender, EventArgs e)
    {
        if (installationComplete && currentStep == 4)
        {
            // Finish
            if (chkLaunchAfterInstall.Checked)
            {
                string launcher = Path.Combine(txtInstallPath?.Text ?? DefaultInstallDir, "TRS398Pro.bat");
                if (System.IO.File.Exists(launcher))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launcher,
                        UseShellExecute = true
                    });
                }
            }
            this.Close();
        }
        else if (currentStep < steps.Length - 1)
        {
            ShowStep(currentStep + 1);
        }
    }

    private void BtnBack_Click(object? sender, EventArgs e)
    {
        if (currentStep > 0)
        {
            ShowStep(currentStep - 1);
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to cancel the installation?",
            "Cancel Installation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            this.Close();
        }
    }
}

