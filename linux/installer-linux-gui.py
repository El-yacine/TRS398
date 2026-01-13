#!/usr/bin/env python3
"""
TRS-398 Pro Linux GUI Installer
Medical Physics Calibration System
Version 2.0.0

Native GTK3 installer for Linux systems.
"""

import os
import sys
import shutil
import subprocess
import threading
import gi

gi.require_version('Gtk', '3.0')
from gi.repository import Gtk, Gdk, GLib, Pango

# Application info
APP_NAME = "TRS-398 Pro"
APP_VERSION = "2.0.0"
APP_PORT = 8000

# Default installation path
DEFAULT_INSTALL_DIR = os.path.expanduser('~/.local/share/trs398-pro')

# Colors
PRIMARY_COLOR = "#10b981"
SECONDARY_COLOR = "#06b6d4"
DARK_COLOR = "#0f172a"
LIGHT_COLOR = "#f8fafc"
MUTED_COLOR = "#94a3b8"


class InstallerWindow(Gtk.Window):
    def __init__(self):
        super().__init__(title=f"{APP_NAME} Setup")
        self.set_default_size(700, 550)
        self.set_resizable(False)
        self.set_position(Gtk.WindowPosition.CENTER)
        
        # Variables
        self.install_dir = DEFAULT_INSTALL_DIR
        self.create_desktop_shortcut = True
        self.create_menu_shortcut = True
        self.launch_after_install = True
        self.accept_license = False
        self.current_step = 0
        self.installation_complete = False
        
        # Apply CSS
        self.apply_css()
        
        # Main container
        self.main_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        self.add(self.main_box)
        
        # Create UI sections
        self.create_header()
        self.create_step_indicator()
        self.create_content_area()
        self.create_footer()
        
        # Show first step
        self.show_step(0)
        
        self.connect("destroy", Gtk.main_quit)
    
    def apply_css(self):
        css = b"""
        window {
            background-color: #f8fafc;
        }
        
        .header {
            background-color: #0f172a;
            padding: 20px;
        }
        
        .header-title {
            color: #10b981;
            font-size: 24px;
            font-weight: bold;
        }
        
        .header-subtitle {
            color: #94a3b8;
            font-size: 11px;
        }
        
        .version-badge {
            background-color: #10b981;
            color: white;
            border-radius: 10px;
            padding: 4px 12px;
            font-size: 9px;
        }
        
        .step-indicator {
            background-color: white;
            padding: 15px;
        }
        
        .step-circle {
            background-color: #94a3b8;
            color: white;
            border-radius: 15px;
            min-width: 30px;
            min-height: 30px;
            font-weight: bold;
        }
        
        .step-circle-active {
            background-color: #06b6d4;
        }
        
        .step-circle-complete {
            background-color: #10b981;
        }
        
        .step-label {
            color: #94a3b8;
            font-size: 9px;
            font-weight: bold;
        }
        
        .step-label-active {
            color: #06b6d4;
        }
        
        .step-label-complete {
            color: #10b981;
        }
        
        .content {
            padding: 30px;
        }
        
        .title {
            color: #0f172a;
            font-size: 18px;
            font-weight: bold;
        }
        
        .description {
            color: #94a3b8;
            font-size: 11px;
        }
        
        .footer {
            background-color: white;
            padding: 15px 20px;
            border-top: 1px solid #e2e8f0;
        }
        
        .btn-primary {
            background-color: #10b981;
            color: white;
            border: none;
            border-radius: 4px;
            padding: 8px 20px;
            font-weight: bold;
        }
        
        .btn-primary:hover {
            background-color: #059669;
        }
        
        .btn-secondary {
            background-color: white;
            color: #64748b;
            border: 1px solid #cbd5e1;
            border-radius: 4px;
            padding: 8px 16px;
        }
        
        .feature-box {
            background-color: white;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 15px;
        }
        
        .feature-title {
            font-weight: bold;
            color: #0f172a;
        }
        
        .feature-desc {
            color: #94a3b8;
            font-size: 10px;
        }
        
        .log-view {
            background-color: #1e293b;
            color: #94a3b8;
            font-family: monospace;
            font-size: 10px;
            padding: 10px;
            border-radius: 4px;
        }
        
        .success-icon {
            color: #10b981;
            font-size: 48px;
            font-weight: bold;
        }
        
        .license-text {
            background-color: white;
            font-family: monospace;
            font-size: 10px;
            padding: 10px;
        }
        """
        
        style_provider = Gtk.CssProvider()
        style_provider.load_from_data(css)
        Gtk.StyleContext.add_provider_for_screen(
            Gdk.Screen.get_default(),
            style_provider,
            Gtk.STYLE_PROVIDER_PRIORITY_APPLICATION
        )
    
    def create_header(self):
        header = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=15)
        header.get_style_context().add_class("header")
        
        # Icon
        icon_label = Gtk.Label(label="⚛")
        icon_label.set_markup('<span size="32000" foreground="#10b981">⚛</span>')
        header.pack_start(icon_label, False, False, 10)
        
        # Title section
        title_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=2)
        
        title = Gtk.Label(label=APP_NAME)
        title.get_style_context().add_class("header-title")
        title.set_halign(Gtk.Align.START)
        title_box.pack_start(title, False, False, 0)
        
        subtitle = Gtk.Label(label="Medical Physics Calibration System")
        subtitle.get_style_context().add_class("header-subtitle")
        subtitle.set_halign(Gtk.Align.START)
        title_box.pack_start(subtitle, False, False, 0)
        
        header.pack_start(title_box, True, True, 0)
        
        # Version badge
        version = Gtk.Label(label=f"v{APP_VERSION}")
        version.get_style_context().add_class("version-badge")
        header.pack_end(version, False, False, 10)
        
        self.main_box.pack_start(header, False, False, 0)
    
    def create_step_indicator(self):
        self.step_box = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=0)
        self.step_box.get_style_context().add_class("step-indicator")
        self.step_box.set_homogeneous(True)
        
        self.step_widgets = []
        steps = ["Welcome", "License", "Location", "Install", "Complete"]
        
        for i, step_name in enumerate(steps):
            step_container = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=5)
            step_container.set_halign(Gtk.Align.CENTER)
            
            # Circle with number
            circle = Gtk.Label(label=str(i + 1))
            circle.set_size_request(30, 30)
            circle.get_style_context().add_class("step-circle")
            
            # Label
            label = Gtk.Label(label=step_name)
            label.get_style_context().add_class("step-label")
            
            step_container.pack_start(circle, False, False, 0)
            step_container.pack_start(label, False, False, 0)
            
            self.step_widgets.append((circle, label))
            self.step_box.pack_start(step_container, True, True, 5)
        
        self.main_box.pack_start(self.step_box, False, False, 0)
    
    def create_content_area(self):
        self.content_frame = Gtk.Frame()
        self.content_frame.set_shadow_type(Gtk.ShadowType.NONE)
        self.content_frame.get_style_context().add_class("content")
        self.main_box.pack_start(self.content_frame, True, True, 0)
    
    def create_footer(self):
        footer = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=10)
        footer.get_style_context().add_class("footer")
        
        self.btn_cancel = Gtk.Button(label="Cancel")
        self.btn_cancel.get_style_context().add_class("btn-secondary")
        self.btn_cancel.connect("clicked", self.on_cancel)
        footer.pack_start(self.btn_cancel, False, False, 0)
        
        self.btn_next = Gtk.Button(label="Next →")
        self.btn_next.get_style_context().add_class("btn-primary")
        self.btn_next.connect("clicked", self.on_next)
        footer.pack_end(self.btn_next, False, False, 0)
        
        self.btn_back = Gtk.Button(label="← Back")
        self.btn_back.get_style_context().add_class("btn-secondary")
        self.btn_back.connect("clicked", self.on_back)
        self.btn_back.set_sensitive(False)
        footer.pack_end(self.btn_back, False, False, 0)
        
        self.main_box.pack_start(footer, False, False, 0)
    
    def update_step_indicators(self):
        for i, (circle, label) in enumerate(self.step_widgets):
            # Remove old classes
            circle.get_style_context().remove_class("step-circle-active")
            circle.get_style_context().remove_class("step-circle-complete")
            label.get_style_context().remove_class("step-label-active")
            label.get_style_context().remove_class("step-label-complete")
            
            if i < self.current_step:
                circle.get_style_context().add_class("step-circle-complete")
                label.get_style_context().add_class("step-label-complete")
            elif i == self.current_step:
                circle.get_style_context().add_class("step-circle-active")
                label.get_style_context().add_class("step-label-active")
    
    def show_step(self, step):
        self.current_step = step
        self.update_step_indicators()
        
        # Clear content
        child = self.content_frame.get_child()
        if child:
            self.content_frame.remove(child)
        
        # Update buttons
        self.btn_back.set_sensitive(step > 0 and step < 4)
        self.btn_cancel.set_sensitive(step < 4)
        
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
        
        self.show_all()
    
    def show_welcome(self):
        self.btn_next.set_label("Next →")
        self.btn_next.set_sensitive(True)
        
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=15)
        box.set_margin_start(10)
        box.set_margin_end(10)
        
        # Title
        title = Gtk.Label(label="Welcome to TRS-398 Pro Setup")
        title.get_style_context().add_class("title")
        title.set_halign(Gtk.Align.START)
        box.pack_start(title, False, False, 0)
        
        # Description
        desc = Gtk.Label(label="This wizard will guide you through the installation of TRS-398 Pro,\na comprehensive photon beam calibration system based on IAEA TRS-398 protocol.")
        desc.get_style_context().add_class("description")
        desc.set_halign(Gtk.Align.START)
        desc.set_line_wrap(True)
        box.pack_start(desc, False, False, 0)
        
        # Features frame
        features_frame = Gtk.Frame(label="Features")
        features_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10)
        features_box.set_margin_top(10)
        features_box.set_margin_bottom(10)
        features_box.set_margin_start(10)
        features_box.set_margin_end(10)
        
        features = [
            ("📊", "Live Calculations", "Real-time dose calculations with automatic kQ factor interpolation"),
            ("🔬", "50+ Ion Chambers", "Pre-configured library with all major chamber models and kQ data"),
            ("📋", "History & Export", "Complete measurement history with CSV and PDF export"),
            ("🔒", "Secure & Local", "All data stored locally with optional authentication"),
        ]
        
        for icon, ftitle, fdesc in features:
            fbox = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=10)
            
            icon_label = Gtk.Label(label=icon)
            icon_label.set_markup(f'<span size="14000">{icon}</span>')
            fbox.pack_start(icon_label, False, False, 5)
            
            text_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=2)
            
            title_label = Gtk.Label(label=ftitle)
            title_label.get_style_context().add_class("feature-title")
            title_label.set_halign(Gtk.Align.START)
            text_box.pack_start(title_label, False, False, 0)
            
            desc_label = Gtk.Label(label=fdesc)
            desc_label.get_style_context().add_class("feature-desc")
            desc_label.set_halign(Gtk.Align.START)
            text_box.pack_start(desc_label, False, False, 0)
            
            fbox.pack_start(text_box, True, True, 0)
            features_box.pack_start(fbox, False, False, 0)
        
        features_frame.add(features_box)
        box.pack_start(features_frame, True, True, 10)
        
        # Continue label
        continue_label = Gtk.Label(label="Click 'Next' to continue with the installation.")
        continue_label.get_style_context().add_class("description")
        continue_label.set_halign(Gtk.Align.START)
        box.pack_start(continue_label, False, False, 0)
        
        self.content_frame.add(box)
    
    def show_license(self):
        self.btn_next.set_label("Next →")
        
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10)
        box.set_margin_start(10)
        box.set_margin_end(10)
        
        # Title
        title = Gtk.Label(label="License Agreement")
        title.get_style_context().add_class("title")
        title.set_halign(Gtk.Align.START)
        box.pack_start(title, False, False, 0)
        
        # Description
        desc = Gtk.Label(label="Please read the following license agreement carefully.")
        desc.get_style_context().add_class("description")
        desc.set_halign(Gtk.Align.START)
        box.pack_start(desc, False, False, 0)
        
        # License text
        scrolled = Gtk.ScrolledWindow()
        scrolled.set_min_content_height(200)
        
        license_text = Gtk.TextView()
        license_text.set_editable(False)
        license_text.set_wrap_mode(Gtk.WrapMode.WORD)
        license_text.get_style_context().add_class("license-text")
        
        buffer = license_text.get_buffer()
        buffer.set_text("""TRS-398 PRO SOFTWARE LICENSE AGREEMENT

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
decisions should be verified by qualified medical physicists.

4. DATA PRIVACY
All measurement data is stored locally on your computer.

By installing this software, you acknowledge that you have read 
and agree to these terms and conditions.""")
        
        scrolled.add(license_text)
        box.pack_start(scrolled, True, True, 10)
        
        # Accept checkbox
        self.license_check = Gtk.CheckButton(label="I accept the terms of the license agreement")
        self.license_check.connect("toggled", self.on_license_toggled)
        box.pack_start(self.license_check, False, False, 0)
        
        self.btn_next.set_sensitive(False)
        
        self.content_frame.add(box)
    
    def show_location(self):
        self.btn_next.set_label("Install →")
        self.btn_next.set_sensitive(True)
        
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10)
        box.set_margin_start(10)
        box.set_margin_end(10)
        
        # Title
        title = Gtk.Label(label="Installation Location")
        title.get_style_context().add_class("title")
        title.set_halign(Gtk.Align.START)
        box.pack_start(title, False, False, 0)
        
        # Description
        desc = Gtk.Label(label="Choose the folder where TRS-398 Pro will be installed.")
        desc.get_style_context().add_class("description")
        desc.set_halign(Gtk.Align.START)
        box.pack_start(desc, False, False, 0)
        
        # Path entry
        path_label = Gtk.Label(label="Installation folder:")
        path_label.set_halign(Gtk.Align.START)
        path_label.set_markup("<b>Installation folder:</b>")
        box.pack_start(path_label, False, False, 5)
        
        path_box = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=10)
        
        self.path_entry = Gtk.Entry()
        self.path_entry.set_text(self.install_dir)
        self.path_entry.connect("changed", self.on_path_changed)
        path_box.pack_start(self.path_entry, True, True, 0)
        
        browse_btn = Gtk.Button(label="Browse...")
        browse_btn.get_style_context().add_class("btn-secondary")
        browse_btn.connect("clicked", self.on_browse)
        path_box.pack_start(browse_btn, False, False, 0)
        
        box.pack_start(path_box, False, False, 0)
        
        # Options frame
        options_frame = Gtk.Frame(label="Options")
        options_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        options_box.set_margin_top(10)
        options_box.set_margin_bottom(10)
        options_box.set_margin_start(10)
        options_box.set_margin_end(10)
        
        self.desktop_check = Gtk.CheckButton(label="Create desktop shortcut")
        self.desktop_check.set_active(True)
        self.desktop_check.connect("toggled", lambda w: setattr(self, 'create_desktop_shortcut', w.get_active()))
        options_box.pack_start(self.desktop_check, False, False, 0)
        
        self.menu_check = Gtk.CheckButton(label="Create application menu shortcut")
        self.menu_check.set_active(True)
        self.menu_check.connect("toggled", lambda w: setattr(self, 'create_menu_shortcut', w.get_active()))
        options_box.pack_start(self.menu_check, False, False, 0)
        
        self.launch_check = Gtk.CheckButton(label="Launch TRS-398 Pro after installation")
        self.launch_check.set_active(True)
        self.launch_check.connect("toggled", lambda w: setattr(self, 'launch_after_install', w.get_active()))
        options_box.pack_start(self.launch_check, False, False, 0)
        
        options_frame.add(options_box)
        box.pack_start(options_frame, False, False, 10)
        
        # Space required
        space_label = Gtk.Label(label="Space required: ~150 MB")
        space_label.get_style_context().add_class("description")
        space_label.set_halign(Gtk.Align.START)
        box.pack_start(space_label, False, False, 0)
        
        self.content_frame.add(box)
    
    def show_install(self):
        self.btn_next.set_label("Installing...")
        self.btn_next.set_sensitive(False)
        self.btn_back.set_sensitive(False)
        self.btn_cancel.set_sensitive(False)
        
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10)
        box.set_margin_start(10)
        box.set_margin_end(10)
        
        # Title
        title = Gtk.Label(label="Installing TRS-398 Pro")
        title.get_style_context().add_class("title")
        title.set_halign(Gtk.Align.START)
        box.pack_start(title, False, False, 0)
        
        # Description
        desc = Gtk.Label(label="Please wait while TRS-398 Pro is being installed...")
        desc.get_style_context().add_class("description")
        desc.set_halign(Gtk.Align.START)
        box.pack_start(desc, False, False, 0)
        
        # Progress bar
        self.progress_bar = Gtk.ProgressBar()
        self.progress_bar.set_show_text(True)
        box.pack_start(self.progress_bar, False, False, 10)
        
        # Status label
        self.status_label = Gtk.Label(label="Preparing installation...")
        self.status_label.get_style_context().add_class("description")
        self.status_label.set_halign(Gtk.Align.START)
        box.pack_start(self.status_label, False, False, 0)
        
        # Log view
        scrolled = Gtk.ScrolledWindow()
        scrolled.set_min_content_height(150)
        
        self.log_view = Gtk.TextView()
        self.log_view.set_editable(False)
        self.log_view.get_style_context().add_class("log-view")
        self.log_view.set_wrap_mode(Gtk.WrapMode.WORD)
        
        scrolled.add(self.log_view)
        box.pack_start(scrolled, True, True, 10)
        
        self.content_frame.add(box)
        
        # Start installation in background
        threading.Thread(target=self.run_installation, daemon=True).start()
    
    def show_complete(self):
        self.btn_next.set_label("Finish")
        self.btn_next.set_sensitive(True)
        self.btn_back.set_sensitive(False)
        self.btn_cancel.set_sensitive(False)
        self.installation_complete = True
        
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=15)
        box.set_margin_start(10)
        box.set_margin_end(10)
        box.set_halign(Gtk.Align.CENTER)
        
        # Success icon
        icon = Gtk.Label()
        icon.set_markup('<span size="48000" foreground="#10b981">✓</span>')
        box.pack_start(icon, False, False, 20)
        
        # Title
        title = Gtk.Label(label="Installation Complete!")
        title.get_style_context().add_class("title")
        box.pack_start(title, False, False, 0)
        
        # Description
        desc = Gtk.Label(label="TRS-398 Pro has been successfully installed on your computer.")
        desc.get_style_context().add_class("description")
        box.pack_start(desc, False, False, 0)
        
        # Info frame
        info_frame = Gtk.Frame(label="Installation Details")
        info_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=5)
        info_box.set_margin_top(10)
        info_box.set_margin_bottom(10)
        info_box.set_margin_start(10)
        info_box.set_margin_end(10)
        
        loc_label = Gtk.Label(label=f"Location: {self.install_dir}")
        loc_label.get_style_context().add_class("description")
        loc_label.set_halign(Gtk.Align.START)
        info_box.pack_start(loc_label, False, False, 0)
        
        url_label = Gtk.Label(label=f"URL: http://localhost:{APP_PORT}")
        url_label.get_style_context().add_class("description")
        url_label.set_halign(Gtk.Align.START)
        info_box.pack_start(url_label, False, False, 0)
        
        info_frame.add(info_box)
        box.pack_start(info_frame, False, False, 10)
        
        # Launch checkbox
        self.final_launch_check = Gtk.CheckButton(label="Launch TRS-398 Pro now")
        self.final_launch_check.set_active(True)
        box.pack_start(self.final_launch_check, False, False, 10)
        
        # Finish label
        finish_label = Gtk.Label(label="Click 'Finish' to complete the setup.")
        finish_label.get_style_context().add_class("description")
        box.pack_start(finish_label, False, False, 0)
        
        self.content_frame.add(box)
    
    def log(self, message, color=None):
        def do_log():
            buffer = self.log_view.get_buffer()
            end_iter = buffer.get_end_iter()
            buffer.insert(end_iter, message + "\n")
            
            # Scroll to end
            adj = self.log_view.get_parent().get_vadjustment()
            adj.set_value(adj.get_upper())
        
        GLib.idle_add(do_log)
    
    def update_progress(self, fraction, status):
        def do_update():
            self.progress_bar.set_fraction(fraction)
            self.progress_bar.set_text(f"{int(fraction * 100)}%")
            self.status_label.set_text(status)
        
        GLib.idle_add(do_update)
    
    def run_installation(self):
        try:
            script_dir = os.path.dirname(os.path.abspath(__file__))
            
            # Step 1: Create directories
            self.update_progress(0.1, "Creating directories...")
            self.log("Creating installation directories...")
            
            os.makedirs(self.install_dir, exist_ok=True)
            os.makedirs(os.path.join(self.install_dir, "wwwroot", "logos"), exist_ok=True)
            os.makedirs(os.path.join(self.install_dir, "data"), exist_ok=True)
            
            self.log(f"✓ Created: {self.install_dir}")
            
            # Step 2: Copy files
            self.update_progress(0.3, "Copying application files...")
            self.log("Copying server files...")
            
            server_dir = os.path.join(script_dir, "server")
            if os.path.exists(server_dir):
                for item in os.listdir(server_dir):
                    src = os.path.join(server_dir, item)
                    dst = os.path.join(self.install_dir, item)
                    if os.path.isdir(src):
                        if os.path.exists(dst):
                            shutil.rmtree(dst)
                        shutil.copytree(src, dst)
                    else:
                        shutil.copy2(src, dst)
                self.log("✓ Server files copied")
            else:
                self.log(f"⚠ Server directory not found: {server_dir}")
            
            # Copy detector library
            detector_lib = os.path.join(script_dir, "detector_library.json")
            if os.path.exists(detector_lib):
                shutil.copy2(detector_lib, self.install_dir)
                self.log("✓ Detector library copied")
            
            # Step 3: Check .NET
            self.update_progress(0.5, "Checking .NET runtime...")
            self.log("Checking for .NET runtime...")
            
            dotnet_path = self.find_dotnet()
            if dotnet_path:
                self.log(f"✓ .NET found: {dotnet_path}")
            else:
                self.log("⚠ .NET not found - will need to be installed separately")
            
            # Step 4: Create launcher
            self.update_progress(0.7, "Creating launcher...")
            self.log("Creating launcher script...")
            self.create_launcher(dotnet_path)
            self.log("✓ Launcher created")
            
            # Step 5: Create shortcuts
            self.update_progress(0.85, "Creating shortcuts...")
            
            if self.create_desktop_shortcut:
                self.log("Creating desktop shortcut...")
                self.create_shortcut("desktop")
                self.log("✓ Desktop shortcut created")
            
            if self.create_menu_shortcut:
                self.log("Creating menu shortcut...")
                self.create_shortcut("menu")
                self.log("✓ Menu shortcut created")
            
            # Step 6: Create uninstaller
            self.update_progress(0.95, "Creating uninstaller...")
            self.log("Creating uninstaller...")
            self.create_uninstaller()
            self.log("✓ Uninstaller created")
            
            # Complete
            self.update_progress(1.0, "Installation complete!")
            self.log("")
            self.log("═" * 50)
            self.log("  Installation completed successfully!")
            self.log("═" * 50)
            
            import time
            time.sleep(1)
            
            GLib.idle_add(lambda: self.show_step(4))
            
        except Exception as e:
            self.log(f"\n✗ Error: {str(e)}")
            self.update_progress(0, f"Installation failed: {str(e)}")
            GLib.idle_add(lambda: self.btn_cancel.set_sensitive(True))
    
    def find_dotnet(self):
        # Check PATH
        dotnet = shutil.which("dotnet")
        if dotnet:
            return dotnet
        
        # Check common locations
        paths = [
            os.path.expanduser("~/.dotnet/dotnet"),
            "/usr/bin/dotnet",
            "/usr/local/bin/dotnet",
        ]
        
        for path in paths:
            if os.path.exists(path):
                return path
        
        return None
    
    def create_launcher(self, dotnet_path):
        launcher_path = os.path.join(self.install_dir, "trs398-pro.sh")
        dotnet_cmd = dotnet_path if dotnet_path else "dotnet"
        
        content = f'''#!/bin/bash
cd "{self.install_dir}"
echo "Starting TRS-398 Pro..."
{dotnet_cmd} run --urls http://localhost:{APP_PORT} &
sleep 3
xdg-open http://localhost:{APP_PORT} 2>/dev/null || echo "Open http://localhost:{APP_PORT} in your browser"
echo "TRS-398 Pro is running. Press Ctrl+C to stop."
wait
'''
        
        with open(launcher_path, 'w') as f:
            f.write(content)
        os.chmod(launcher_path, 0o755)
        
        # Create symlink in ~/.local/bin
        bin_dir = os.path.expanduser("~/.local/bin")
        os.makedirs(bin_dir, exist_ok=True)
        symlink = os.path.join(bin_dir, "trs398-pro")
        if os.path.exists(symlink):
            os.remove(symlink)
        os.symlink(launcher_path, symlink)
    
    def create_shortcut(self, location):
        if location == "desktop":
            shortcut_dir = os.path.expanduser("~/Desktop")
        else:
            shortcut_dir = os.path.expanduser("~/.local/share/applications")
        
        os.makedirs(shortcut_dir, exist_ok=True)
        shortcut_path = os.path.join(shortcut_dir, "trs398-pro.desktop")
        
        content = f'''[Desktop Entry]
Version=1.0
Type=Application
Name=TRS-398 Pro
Comment=Medical Physics Calibration System
Exec={self.install_dir}/trs398-pro.sh
Icon=applications-science
Terminal=false
Categories=Science;Medical;Education;
'''
        
        with open(shortcut_path, 'w') as f:
            f.write(content)
        os.chmod(shortcut_path, 0o755)
    
    def create_uninstaller(self):
        uninstaller_path = os.path.join(self.install_dir, "uninstall.sh")
        desktop_shortcut = os.path.expanduser("~/Desktop/trs398-pro.desktop")
        menu_shortcut = os.path.expanduser("~/.local/share/applications/trs398-pro.desktop")
        bin_symlink = os.path.expanduser("~/.local/bin/trs398-pro")
        
        content = f'''#!/bin/bash
echo "TRS-398 Pro Uninstaller"
echo "======================="
echo ""
read -p "Are you sure you want to uninstall TRS-398 Pro? (y/N): " confirm
if [ "$confirm" != "y" ] && [ "$confirm" != "Y" ]; then
    echo "Uninstall cancelled."
    exit 0
fi

echo ""
echo "Removing application files..."
rm -rf "{self.install_dir}"
rm -f "{desktop_shortcut}"
rm -f "{menu_shortcut}"
rm -f "{bin_symlink}"

echo ""
echo "TRS-398 Pro has been uninstalled."
'''
        
        with open(uninstaller_path, 'w') as f:
            f.write(content)
        os.chmod(uninstaller_path, 0o755)
    
    def on_license_toggled(self, widget):
        self.accept_license = widget.get_active()
        self.btn_next.set_sensitive(self.accept_license)
    
    def on_path_changed(self, widget):
        self.install_dir = widget.get_text()
    
    def on_browse(self, widget):
        dialog = Gtk.FileChooserDialog(
            title="Select Installation Folder",
            parent=self,
            action=Gtk.FileChooserAction.SELECT_FOLDER
        )
        dialog.add_buttons(
            Gtk.STOCK_CANCEL, Gtk.ResponseType.CANCEL,
            Gtk.STOCK_OPEN, Gtk.ResponseType.OK
        )
        dialog.set_current_folder(self.install_dir)
        
        response = dialog.run()
        if response == Gtk.ResponseType.OK:
            self.install_dir = dialog.get_filename()
            self.path_entry.set_text(self.install_dir)
        
        dialog.destroy()
    
    def on_next(self, widget):
        if self.installation_complete and self.current_step == 4:
            # Finish
            if self.final_launch_check.get_active():
                launcher = os.path.join(self.install_dir, "trs398-pro.sh")
                if os.path.exists(launcher):
                    subprocess.Popen([launcher], start_new_session=True)
            Gtk.main_quit()
        elif self.current_step < 4:
            self.show_step(self.current_step + 1)
    
    def on_back(self, widget):
        if self.current_step > 0:
            self.show_step(self.current_step - 1)
    
    def on_cancel(self, widget):
        dialog = Gtk.MessageDialog(
            parent=self,
            flags=0,
            message_type=Gtk.MessageType.QUESTION,
            buttons=Gtk.ButtonsType.YES_NO,
            text="Cancel Installation?"
        )
        dialog.format_secondary_text("Are you sure you want to cancel the installation?")
        response = dialog.run()
        dialog.destroy()
        
        if response == Gtk.ResponseType.YES:
            Gtk.main_quit()


def main():
    win = InstallerWindow()
    win.show_all()
    Gtk.main()


if __name__ == "__main__":
    main()

