#!/usr/bin/env python3
"""
TRS-398 Pro GUI Installer
Medical Physics Calibration System
Version 2.0.0

A graphical installer for TRS-398 Pro that works on Windows and Linux.
Uses Tkinter for cross-platform compatibility.
"""

import os
import sys
import shutil
import subprocess
import threading
import platform
from pathlib import Path

# Try to import tkinter
try:
    import tkinter as tk
    from tkinter import ttk, messagebox, filedialog
    from tkinter import font as tkfont
except ImportError:
    print("Error: tkinter is not installed.")
    print("On Ubuntu/Debian: sudo apt install python3-tk")
    print("On Fedora: sudo dnf install python3-tkinter")
    sys.exit(1)

# Application info
APP_NAME = "TRS-398 Pro"
APP_VERSION = "2.0.0"
APP_PORT = 8000

# Detect OS
IS_WINDOWS = platform.system() == "Windows"
IS_LINUX = platform.system() == "Linux"
IS_MAC = platform.system() == "Darwin"

# Default installation paths
if IS_WINDOWS:
    DEFAULT_INSTALL_DIR = os.path.join(os.environ.get('LOCALAPPDATA', ''), 'TRS398Pro')
else:
    DEFAULT_INSTALL_DIR = os.path.expanduser('~/.local/share/trs398-pro')


class InstallerApp:
    def __init__(self, root):
        self.root = root
        self.root.title(f"{APP_NAME} Installer")
        self.root.geometry("700x550")
        self.root.resizable(False, False)
        
        # Center window
        self.center_window()
        
        # Variables
        self.install_dir = tk.StringVar(value=DEFAULT_INSTALL_DIR)
        self.create_desktop_shortcut = tk.BooleanVar(value=True)
        self.create_menu_shortcut = tk.BooleanVar(value=True)
        self.launch_after_install = tk.BooleanVar(value=True)
        self.accept_license = tk.BooleanVar(value=False)
        
        # Current step
        self.current_step = 0
        self.steps = ['welcome', 'license', 'location', 'install', 'complete']
        
        # Setup styles
        self.setup_styles()
        
        # Create main container
        self.main_frame = ttk.Frame(root, padding=20)
        self.main_frame.pack(fill=tk.BOTH, expand=True)
        
        # Header
        self.create_header()
        
        # Content area
        self.content_frame = ttk.Frame(self.main_frame)
        self.content_frame.pack(fill=tk.BOTH, expand=True, pady=20)
        
        # Navigation buttons
        self.create_navigation()
        
        # Show first step
        self.show_step(0)
    
    def center_window(self):
        self.root.update_idletasks()
        width = 700
        height = 550
        x = (self.root.winfo_screenwidth() // 2) - (width // 2)
        y = (self.root.winfo_screenheight() // 2) - (height // 2)
        self.root.geometry(f'{width}x{height}+{x}+{y}')
    
    def setup_styles(self):
        style = ttk.Style()
        
        # Try to use a modern theme
        available_themes = style.theme_names()
        if 'clam' in available_themes:
            style.theme_use('clam')
        elif 'vista' in available_themes:
            style.theme_use('vista')
        
        # Custom styles
        style.configure('Header.TLabel', font=('Helvetica', 24, 'bold'), foreground='#10b981')
        style.configure('SubHeader.TLabel', font=('Helvetica', 11), foreground='#64748b')
        style.configure('Step.TLabel', font=('Helvetica', 10, 'bold'))
        style.configure('Title.TLabel', font=('Helvetica', 16, 'bold'), foreground='#1e293b')
        style.configure('Info.TLabel', font=('Helvetica', 10), foreground='#64748b')
        style.configure('Success.TLabel', font=('Helvetica', 12), foreground='#10b981')
        
        # Button styles
        style.configure('Primary.TButton', font=('Helvetica', 10, 'bold'))
        style.configure('Nav.TButton', font=('Helvetica', 10))
    
    def create_header(self):
        header_frame = ttk.Frame(self.main_frame)
        header_frame.pack(fill=tk.X)
        
        # Logo/Title
        title_frame = ttk.Frame(header_frame)
        title_frame.pack(side=tk.LEFT)
        
        ttk.Label(title_frame, text="⚛", font=('Helvetica', 32), foreground='#10b981').pack(side=tk.LEFT, padx=(0, 10))
        
        text_frame = ttk.Frame(title_frame)
        text_frame.pack(side=tk.LEFT)
        ttk.Label(text_frame, text=APP_NAME, style='Header.TLabel').pack(anchor=tk.W)
        ttk.Label(text_frame, text="Medical Physics Calibration System", style='SubHeader.TLabel').pack(anchor=tk.W)
        
        # Version badge
        ttk.Label(header_frame, text=f"v{APP_VERSION}", font=('Helvetica', 9), 
                  foreground='white', background='#10b981', padding=(8, 4)).pack(side=tk.RIGHT)
        
        # Separator
        ttk.Separator(self.main_frame, orient=tk.HORIZONTAL).pack(fill=tk.X, pady=15)
        
        # Step indicators
        self.step_frame = ttk.Frame(self.main_frame)
        self.step_frame.pack(fill=tk.X)
        
        self.step_labels = []
        step_names = ['Welcome', 'License', 'Location', 'Install', 'Complete']
        for i, name in enumerate(step_names):
            frame = ttk.Frame(self.step_frame)
            frame.pack(side=tk.LEFT, expand=True)
            
            circle = ttk.Label(frame, text=str(i+1), font=('Helvetica', 10, 'bold'),
                              foreground='white', background='#94a3b8', width=3, anchor=tk.CENTER)
            circle.pack()
            label = ttk.Label(frame, text=name, style='Step.TLabel')
            label.pack()
            
            self.step_labels.append((circle, label))
    
    def create_navigation(self):
        nav_frame = ttk.Frame(self.main_frame)
        nav_frame.pack(fill=tk.X, side=tk.BOTTOM)
        
        ttk.Separator(self.main_frame, orient=tk.HORIZONTAL).pack(fill=tk.X, side=tk.BOTTOM, pady=10)
        
        self.cancel_btn = ttk.Button(nav_frame, text="Cancel", command=self.cancel, style='Nav.TButton')
        self.cancel_btn.pack(side=tk.LEFT)
        
        self.next_btn = ttk.Button(nav_frame, text="Next →", command=self.next_step, style='Primary.TButton')
        self.next_btn.pack(side=tk.RIGHT)
        
        self.back_btn = ttk.Button(nav_frame, text="← Back", command=self.prev_step, style='Nav.TButton')
        self.back_btn.pack(side=tk.RIGHT, padx=10)
    
    def update_step_indicators(self):
        for i, (circle, label) in enumerate(self.step_labels):
            if i < self.current_step:
                circle.configure(background='#10b981')
            elif i == self.current_step:
                circle.configure(background='#3b82f6')
            else:
                circle.configure(background='#94a3b8')
    
    def clear_content(self):
        for widget in self.content_frame.winfo_children():
            widget.destroy()
    
    def show_step(self, step):
        self.current_step = step
        self.clear_content()
        self.update_step_indicators()
        
        # Update navigation buttons
        self.back_btn.configure(state=tk.NORMAL if step > 0 else tk.DISABLED)
        
        if step == 0:
            self.show_welcome()
        elif step == 1:
            self.show_license()
        elif step == 2:
            self.show_location()
        elif step == 3:
            self.show_install()
        elif step == 4:
            self.show_complete()
    
    def show_welcome(self):
        self.next_btn.configure(text="Next →", state=tk.NORMAL)
        
        ttk.Label(self.content_frame, text="Welcome to TRS-398 Pro Setup", 
                  style='Title.TLabel').pack(anchor=tk.W, pady=(0, 10))
        
        ttk.Label(self.content_frame, 
                  text="This wizard will guide you through the installation of TRS-398 Pro,\n"
                       "a comprehensive photon beam calibration system based on IAEA TRS-398 protocol.",
                  style='Info.TLabel', wraplength=600, justify=tk.LEFT).pack(anchor=tk.W, pady=(0, 20))
        
        # Features
        features_frame = ttk.LabelFrame(self.content_frame, text="Features", padding=15)
        features_frame.pack(fill=tk.X, pady=10)
        
        features = [
            ("📊", "Live Calculations", "Real-time dose calculations with automatic kQ factor interpolation"),
            ("🔬", "50+ Ion Chambers", "Pre-configured library with all major chamber models and kQ data"),
            ("📋", "History & Export", "Complete measurement history with CSV and PDF export"),
            ("🔒", "Secure & Local", "All data stored locally with optional authentication"),
        ]
        
        for icon, title, desc in features:
            f = ttk.Frame(features_frame)
            f.pack(fill=tk.X, pady=5)
            ttk.Label(f, text=icon, font=('Helvetica', 16)).pack(side=tk.LEFT, padx=(0, 10))
            text_f = ttk.Frame(f)
            text_f.pack(side=tk.LEFT)
            ttk.Label(text_f, text=title, font=('Helvetica', 10, 'bold')).pack(anchor=tk.W)
            ttk.Label(text_f, text=desc, style='Info.TLabel').pack(anchor=tk.W)
        
        ttk.Label(self.content_frame, 
                  text="Click 'Next' to continue with the installation.",
                  style='Info.TLabel').pack(anchor=tk.W, pady=(20, 0))
    
    def show_license(self):
        self.next_btn.configure(text="Next →")
        
        ttk.Label(self.content_frame, text="License Agreement", 
                  style='Title.TLabel').pack(anchor=tk.W, pady=(0, 10))
        
        ttk.Label(self.content_frame, 
                  text="Please read the following license agreement carefully.",
                  style='Info.TLabel').pack(anchor=tk.W, pady=(0, 10))
        
        # License text
        license_frame = ttk.Frame(self.content_frame)
        license_frame.pack(fill=tk.BOTH, expand=True, pady=10)
        
        license_text = tk.Text(license_frame, wrap=tk.WORD, height=12, font=('Courier', 9))
        scrollbar = ttk.Scrollbar(license_frame, orient=tk.VERTICAL, command=license_text.yview)
        license_text.configure(yscrollcommand=scrollbar.set)
        
        license_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        license_content = """TRS-398 PRO SOFTWARE LICENSE AGREEMENT

Copyright (c) 2024 TRS-398 Pro Team
All rights reserved.

TERMS AND CONDITIONS

1. GRANT OF LICENSE
This software is provided for use in medical physics calibration 
according to IAEA TRS-398 protocol. You may install and use this 
software on any number of computers.

2. DISCLAIMER OF WARRANTY
THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND.
The authors are not responsible for any damages arising from the 
use of this software.

3. MEDICAL DISCLAIMER
This software is intended as a calculation aid only. All clinical 
decisions should be verified by qualified medical physicists. The 
user assumes full responsibility for the clinical use of results.

4. DATA PRIVACY
All measurement data is stored locally on your computer. No data 
is transmitted to external servers.

5. REDISTRIBUTION
You may freely distribute this software provided this license 
agreement is included.

By installing this software, you acknowledge that you have read 
and agree to these terms and conditions.
"""
        license_text.insert(tk.END, license_content)
        license_text.configure(state=tk.DISABLED)
        
        # Accept checkbox
        ttk.Checkbutton(self.content_frame, 
                        text="I accept the terms of the license agreement",
                        variable=self.accept_license,
                        command=self.check_license).pack(anchor=tk.W, pady=(10, 0))
        
        self.check_license()
    
    def check_license(self):
        if self.accept_license.get():
            self.next_btn.configure(state=tk.NORMAL)
        else:
            self.next_btn.configure(state=tk.DISABLED)
    
    def show_location(self):
        self.next_btn.configure(text="Install →", state=tk.NORMAL)
        
        ttk.Label(self.content_frame, text="Installation Location", 
                  style='Title.TLabel').pack(anchor=tk.W, pady=(0, 10))
        
        ttk.Label(self.content_frame, 
                  text="Choose the folder where TRS-398 Pro will be installed.",
                  style='Info.TLabel').pack(anchor=tk.W, pady=(0, 20))
        
        # Directory selection
        dir_frame = ttk.Frame(self.content_frame)
        dir_frame.pack(fill=tk.X, pady=10)
        
        ttk.Label(dir_frame, text="Installation folder:").pack(anchor=tk.W)
        
        entry_frame = ttk.Frame(dir_frame)
        entry_frame.pack(fill=tk.X, pady=5)
        
        ttk.Entry(entry_frame, textvariable=self.install_dir, width=60).pack(side=tk.LEFT, fill=tk.X, expand=True)
        ttk.Button(entry_frame, text="Browse...", command=self.browse_dir).pack(side=tk.RIGHT, padx=(10, 0))
        
        # Options
        options_frame = ttk.LabelFrame(self.content_frame, text="Options", padding=15)
        options_frame.pack(fill=tk.X, pady=20)
        
        ttk.Checkbutton(options_frame, text="Create desktop shortcut", 
                        variable=self.create_desktop_shortcut).pack(anchor=tk.W, pady=2)
        ttk.Checkbutton(options_frame, text="Create Start Menu / Application Menu shortcut", 
                        variable=self.create_menu_shortcut).pack(anchor=tk.W, pady=2)
        ttk.Checkbutton(options_frame, text="Launch TRS-398 Pro after installation", 
                        variable=self.launch_after_install).pack(anchor=tk.W, pady=2)
        
        # Space required
        ttk.Label(self.content_frame, text="Space required: ~150 MB", style='Info.TLabel').pack(anchor=tk.W)
    
    def browse_dir(self):
        directory = filedialog.askdirectory(initialdir=self.install_dir.get())
        if directory:
            self.install_dir.set(directory)
    
    def show_install(self):
        self.next_btn.configure(text="Installing...", state=tk.DISABLED)
        self.back_btn.configure(state=tk.DISABLED)
        self.cancel_btn.configure(state=tk.DISABLED)
        
        ttk.Label(self.content_frame, text="Installing TRS-398 Pro", 
                  style='Title.TLabel').pack(anchor=tk.W, pady=(0, 10))
        
        ttk.Label(self.content_frame, 
                  text="Please wait while TRS-398 Pro is being installed...",
                  style='Info.TLabel').pack(anchor=tk.W, pady=(0, 20))
        
        # Progress bar
        self.progress = ttk.Progressbar(self.content_frame, length=600, mode='determinate')
        self.progress.pack(fill=tk.X, pady=10)
        
        # Status label
        self.status_label = ttk.Label(self.content_frame, text="Preparing installation...", style='Info.TLabel')
        self.status_label.pack(anchor=tk.W, pady=5)
        
        # Log area
        log_frame = ttk.Frame(self.content_frame)
        log_frame.pack(fill=tk.BOTH, expand=True, pady=10)
        
        self.log_text = tk.Text(log_frame, wrap=tk.WORD, height=8, font=('Courier', 9), state=tk.DISABLED)
        scrollbar = ttk.Scrollbar(log_frame, orient=tk.VERTICAL, command=self.log_text.yview)
        self.log_text.configure(yscrollcommand=scrollbar.set)
        
        self.log_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        # Start installation in background thread
        threading.Thread(target=self.run_installation, daemon=True).start()
    
    def log(self, message):
        self.log_text.configure(state=tk.NORMAL)
        self.log_text.insert(tk.END, message + "\n")
        self.log_text.see(tk.END)
        self.log_text.configure(state=tk.DISABLED)
        self.root.update()
    
    def update_progress(self, value, status):
        self.progress['value'] = value
        self.status_label.configure(text=status)
        self.root.update()
    
    def run_installation(self):
        try:
            install_dir = Path(self.install_dir.get())
            script_dir = Path(__file__).parent.resolve()
            
            # Step 1: Create directories
            self.update_progress(10, "Creating directories...")
            self.log("Creating installation directories...")
            install_dir.mkdir(parents=True, exist_ok=True)
            (install_dir / "wwwroot" / "logos").mkdir(parents=True, exist_ok=True)
            (install_dir / "data").mkdir(parents=True, exist_ok=True)
            self.log(f"✓ Created: {install_dir}")
            
            # Step 2: Copy files
            self.update_progress(30, "Copying application files...")
            self.log("Copying server files...")
            
            server_dir = script_dir / "server"
            if server_dir.exists():
                for item in server_dir.iterdir():
                    dest = install_dir / item.name
                    if item.is_dir():
                        if dest.exists():
                            shutil.rmtree(dest)
                        shutil.copytree(item, dest)
                    else:
                        shutil.copy2(item, dest)
                self.log("✓ Server files copied")
            else:
                raise FileNotFoundError(f"Server directory not found: {server_dir}")
            
            # Copy detector library
            detector_lib = script_dir / "detector_library.json"
            if detector_lib.exists():
                shutil.copy2(detector_lib, install_dir)
                self.log("✓ Detector library copied")
            
            # Step 3: Check .NET
            self.update_progress(50, "Checking .NET runtime...")
            self.log("Checking for .NET runtime...")
            
            dotnet_path = self.find_dotnet()
            if dotnet_path:
                self.log(f"✓ .NET found: {dotnet_path}")
            else:
                self.log("⚠ .NET not found - will need to be installed separately")
            
            # Step 4: Build application
            self.update_progress(60, "Building application...")
            self.log("Building application...")
            
            if dotnet_path:
                try:
                    result = subprocess.run(
                        [dotnet_path, "build", "--configuration", "Release"],
                        cwd=str(install_dir),
                        capture_output=True,
                        text=True,
                        timeout=120
                    )
                    if result.returncode == 0:
                        self.log("✓ Application built successfully")
                    else:
                        self.log(f"⚠ Build warning: {result.stderr[:200] if result.stderr else 'Unknown'}")
                except Exception as e:
                    self.log(f"⚠ Build skipped: {e}")
            
            # Step 5: Create launcher
            self.update_progress(75, "Creating launcher...")
            self.log("Creating launcher script...")
            self.create_launcher_script(install_dir, dotnet_path)
            self.log("✓ Launcher created")
            
            # Step 6: Create shortcuts
            self.update_progress(85, "Creating shortcuts...")
            
            if self.create_desktop_shortcut.get():
                self.log("Creating desktop shortcut...")
                self.create_shortcut(install_dir, "desktop")
                self.log("✓ Desktop shortcut created")
            
            if self.create_menu_shortcut.get():
                self.log("Creating menu shortcut...")
                self.create_shortcut(install_dir, "menu")
                self.log("✓ Menu shortcut created")
            
            # Step 7: Complete
            self.update_progress(100, "Installation complete!")
            self.log("")
            self.log("=" * 50)
            self.log("Installation completed successfully!")
            self.log("=" * 50)
            
            # Move to complete step
            self.root.after(1000, lambda: self.show_step(4))
            
        except Exception as e:
            self.log(f"\n✗ Error: {str(e)}")
            self.update_progress(0, f"Installation failed: {str(e)}")
            messagebox.showerror("Installation Error", f"Installation failed:\n\n{str(e)}")
            self.cancel_btn.configure(state=tk.NORMAL)
    
    def find_dotnet(self):
        """Find dotnet executable"""
        # Check PATH
        dotnet = shutil.which("dotnet")
        if dotnet:
            return dotnet
        
        # Check common locations
        if IS_WINDOWS:
            paths = [
                os.path.expandvars(r"%ProgramFiles%\dotnet\dotnet.exe"),
                os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"),
            ]
        else:
            paths = [
                os.path.expanduser("~/.dotnet/dotnet"),
                "/usr/bin/dotnet",
                "/usr/local/bin/dotnet",
            ]
        
        for path in paths:
            if os.path.exists(path):
                return path
        
        return None
    
    def create_launcher_script(self, install_dir, dotnet_path):
        """Create platform-specific launcher script"""
        if IS_WINDOWS:
            launcher = install_dir / "TRS398Pro.bat"
            dotnet_cmd = dotnet_path if dotnet_path else "dotnet"
            content = f'''@echo off
cd /d "{install_dir}"
echo Starting TRS-398 Pro...
start /B "{dotnet_cmd}" run --urls http://localhost:{APP_PORT}
timeout /t 3 /nobreak >nul
start http://localhost:{APP_PORT}
echo TRS-398 Pro is running at http://localhost:{APP_PORT}
echo Close this window to stop the server.
pause >nul
'''
            launcher.write_text(content)
        else:
            launcher = install_dir / "trs398-pro.sh"
            dotnet_cmd = dotnet_path if dotnet_path else "dotnet"
            content = f'''#!/bin/bash
cd "{install_dir}"
echo "Starting TRS-398 Pro..."
{dotnet_cmd} run --urls http://localhost:{APP_PORT} &
sleep 3
xdg-open http://localhost:{APP_PORT} 2>/dev/null || echo "Open http://localhost:{APP_PORT} in your browser"
echo "TRS-398 Pro is running. Press Ctrl+C to stop."
wait
'''
            launcher.write_text(content)
            os.chmod(launcher, 0o755)
            
            # Also create symlink in ~/.local/bin
            bin_dir = Path.home() / ".local" / "bin"
            bin_dir.mkdir(parents=True, exist_ok=True)
            symlink = bin_dir / "trs398-pro"
            if symlink.exists():
                symlink.unlink()
            symlink.symlink_to(launcher)
    
    def create_shortcut(self, install_dir, location):
        """Create desktop or menu shortcut"""
        if IS_WINDOWS:
            self.create_windows_shortcut(install_dir, location)
        else:
            self.create_linux_shortcut(install_dir, location)
    
    def create_windows_shortcut(self, install_dir, location):
        """Create Windows shortcut using PowerShell"""
        if location == "desktop":
            shortcut_dir = Path.home() / "Desktop"
        else:
            shortcut_dir = Path(os.environ.get('APPDATA', '')) / "Microsoft" / "Windows" / "Start Menu" / "Programs"
        
        shortcut_path = shortcut_dir / "TRS-398 Pro.lnk"
        target = install_dir / "TRS398Pro.bat"
        
        ps_script = f'''
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("{shortcut_path}")
$Shortcut.TargetPath = "{target}"
$Shortcut.WorkingDirectory = "{install_dir}"
$Shortcut.Description = "TRS-398 Pro - Medical Physics Calibration System"
$Shortcut.Save()
'''
        subprocess.run(["powershell", "-Command", ps_script], capture_output=True)
    
    def create_linux_shortcut(self, install_dir, location):
        """Create Linux .desktop file"""
        if location == "desktop":
            shortcut_dir = Path.home() / "Desktop"
        else:
            shortcut_dir = Path.home() / ".local" / "share" / "applications"
        
        shortcut_dir.mkdir(parents=True, exist_ok=True)
        shortcut_path = shortcut_dir / "trs398-pro.desktop"
        
        content = f'''[Desktop Entry]
Version=1.0
Type=Application
Name=TRS-398 Pro
Comment=Medical Physics Calibration System
Exec={install_dir}/trs398-pro.sh
Icon=applications-science
Terminal=false
Categories=Science;Medical;Education;
'''
        shortcut_path.write_text(content)
        os.chmod(shortcut_path, 0o755)
    
    def show_complete(self):
        self.next_btn.configure(text="Finish", state=tk.NORMAL, command=self.finish)
        self.back_btn.configure(state=tk.DISABLED)
        self.cancel_btn.configure(state=tk.DISABLED)
        
        ttk.Label(self.content_frame, text="✓", font=('Helvetica', 48), 
                  foreground='#10b981').pack(pady=(20, 10))
        
        ttk.Label(self.content_frame, text="Installation Complete!", 
                  style='Title.TLabel').pack(pady=(0, 10))
        
        ttk.Label(self.content_frame, 
                  text="TRS-398 Pro has been successfully installed on your computer.",
                  style='Info.TLabel').pack(pady=(0, 20))
        
        # Installation info
        info_frame = ttk.LabelFrame(self.content_frame, text="Installation Details", padding=15)
        info_frame.pack(fill=tk.X, pady=10)
        
        ttk.Label(info_frame, text=f"Location: {self.install_dir.get()}", style='Info.TLabel').pack(anchor=tk.W)
        ttk.Label(info_frame, text=f"URL: http://localhost:{APP_PORT}", style='Info.TLabel').pack(anchor=tk.W)
        
        # Launch option
        ttk.Checkbutton(self.content_frame, 
                        text="Launch TRS-398 Pro now",
                        variable=self.launch_after_install).pack(anchor=tk.W, pady=20)
        
        ttk.Label(self.content_frame, 
                  text="Click 'Finish' to complete the setup.",
                  style='Info.TLabel').pack(anchor=tk.W)
    
    def next_step(self):
        if self.current_step < len(self.steps) - 1:
            self.show_step(self.current_step + 1)
    
    def prev_step(self):
        if self.current_step > 0:
            self.show_step(self.current_step - 1)
    
    def cancel(self):
        if messagebox.askyesno("Cancel Installation", "Are you sure you want to cancel the installation?"):
            self.root.destroy()
    
    def finish(self):
        if self.launch_after_install.get():
            install_dir = Path(self.install_dir.get())
            if IS_WINDOWS:
                launcher = install_dir / "TRS398Pro.bat"
                os.startfile(str(launcher))
            else:
                launcher = install_dir / "trs398-pro.sh"
                subprocess.Popen([str(launcher)], start_new_session=True)
        
        self.root.destroy()


def main():
    root = tk.Tk()
    
    # Set icon if available
    try:
        if IS_WINDOWS:
            root.iconbitmap(default='')
    except:
        pass
    
    app = InstallerApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()

