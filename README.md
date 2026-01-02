# TRS-398 Calibration Application

A modern, user-friendly web application for performing TRS-398 photon and electron beam calibration measurements. This tool helps medical physicists calculate absorbed dose calibrations following the IAEA TRS-398 protocol.

## 🌟 Features

- **Easy-to-use interface** - Clean, intuitive design that feels familiar
- **Automatic calculations** - Real-time computation of correction factors (Ktp, Kpol, Ks) and dose results
- **Chamber library** - Pre-loaded detector library with automatic kQ selection based on TPR/R50
- **History tracking** - View and manage all your past measurements
- **PDF reports** - Generate professional calibration reports with your hospital logo
- **CSV export** - Export data for further analysis
- **Mobile-friendly** - Works great on tablets and phones too

## 📋 Requirements

Before you start, make sure you have:

- **.NET 8.0 SDK** or later ([Download here](https://dotnet.microsoft.com/download))
- **A web browser** (Chrome, Firefox, Edge, or Safari)
- **For Apache hosting**: Apache web server with mod_proxy enabled

## 🚀 Quick Start (Standalone Mode)

The easiest way to run the application is in standalone mode. This works on both Windows and Linux.

### On Windows

1. **Download and extract** the project to a folder (e.g., `C:\TRS398_Clean`)

2. **Open PowerShell or Command Prompt** in the project folder

3. **Run the application:**
   ```powershell
   cd server
   dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:5000
   ```

4. **Open your browser** and go to: `http://localhost:5000`

That's it! The application is now running. You can start making measurements right away.

### On Linux

1. **Extract the project** to a folder (e.g., `/home/username/TRS398_Clean`)

2. **Open a terminal** and navigate to the project folder

3. **Run the application:**
   ```bash
   cd server
   dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:5000
   ```

4. **Open your browser** and go to: `http://localhost:5000`

The application will automatically create the database on first run. No additional setup needed!

## 🌐 Hosting on Apache Server

If you want to host the application on an Apache web server (like you might do in a hospital network), follow these steps:

### Step 1: Build the Application

First, build the application for production:

**On Windows:**
```powershell
cd server
dotnet publish -c Release -o ../publish
```

**On Linux:**
```bash
cd server
dotnet publish -c Release -o ../publish
```

This creates a `publish` folder with all the files needed to run the application.

### Step 2: Set Up the Application Service

You'll want the application to run as a background service. Here's how:

#### On Linux (using systemd)

1. **Create a service file:**
   ```bash
   sudo nano /etc/systemd/system/trs398.service
   ```

2. **Add this content** (adjust paths to match your setup):
   ```ini
   [Unit]
   Description=TRS-398 Calibration Application
   After=network.target

   [Service]
   Type=notify
   WorkingDirectory=/path/to/TRS398_Clean/publish
   ExecStart=/usr/bin/dotnet /path/to/TRS398_Clean/publish/MyQC.WebAPI.dll --urls http://localhost:5000
   Restart=always
   RestartSec=10
   User=www-data
   Environment=ASPNETCORE_ENVIRONMENT=Production

   [Install]
   WantedBy=multi-user.target
   ```

3. **Replace `/path/to/TRS398_Clean`** with your actual path

4. **Enable and start the service:**
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable trs398
   sudo systemctl start trs398
   ```

5. **Check if it's running:**
   ```bash
   sudo systemctl status trs398
   ```

#### On Windows (using Task Scheduler or NSSM)

You can use NSSM (Non-Sucking Service Manager) to create a Windows service:

1. **Download NSSM** from [nssm.cc](https://nssm.cc/download)

2. **Install the service:**
   ```powershell
   nssm install TRS398 "C:\Program Files\dotnet\dotnet.exe" "C:\path\to\TRS398_Clean\publish\MyQC.WebAPI.dll --urls http://localhost:5000"
   ```

3. **Start the service:**
   ```powershell
   nssm start TRS398
   ```

### Step 3: Configure Apache

Now configure Apache to forward requests to your application:

1. **Enable required Apache modules:**
   ```bash
   sudo a2enmod proxy
   sudo a2enmod proxy_http
   sudo a2enmod rewrite
   ```

2. **Create a virtual host configuration:**
   ```bash
   sudo nano /etc/apache2/sites-available/trs398.conf
   ```

3. **Add this configuration:**
   ```apache
   <VirtualHost *:80>
       ServerName your-domain.com
       # Or use: ServerName localhost (for local access)
       
       # Proxy all requests to the .NET application
       ProxyPreserveHost On
       ProxyPass / http://localhost:5000/
       ProxyPassReverse / http://localhost:5000/
       
       # Optional: Log file location
       ErrorLog ${APACHE_LOG_DIR}/trs398_error.log
       CustomLog ${APACHE_LOG_DIR}/trs398_access.log combined
   </VirtualHost>
   ```

4. **Enable the site:**
   ```bash
   sudo a2ensite trs398.conf
   sudo systemctl reload apache2
   ```

5. **Test the configuration:**
   ```bash
   sudo apache2ctl configtest
   ```

### Step 4: Access Your Application

Now you can access the application through Apache:
- **Local network**: `http://your-server-ip`
- **Domain**: `http://your-domain.com`

The application will be accessible to anyone on your network (or the internet, if configured).

## 📁 Project Structure

```
TRS398_Clean/
├── server/                 # Main application folder
│   ├── wwwroot/           # Web interface files
│   │   ├── index.html     # Main measurement page
│   │   ├── history.html   # History view
│   │   └── logos/         # Hospital logos for PDF reports
│   ├── Data/              # Database context
│   ├── Models/            # Data models
│   ├── Services/          # Business logic
│   └── trs398.db         # SQLite database (created automatically)
├── detector_library.json  # Chamber detector library
└── README.md             # This file
```

## 🎯 How to Use

### Making a Measurement

1. **Fill in the basic information:**
   - Select your chamber from the dropdown
   - Enter the energy (e.g., 6X, 10X, 15X, 18X)
   - For photons: Enter TPR20,10 (kQ will be calculated automatically)
   - For electrons: Enter R50 (kQ will be calculated automatically)

2. **Enter environmental conditions:**
   - Temperature (T) in °C
   - Pressure (P) in mBar

3. **Take your measurements:**
   - **M+**: Three readings with positive polarity (+300V)
   - **M-**: Three readings with negative polarity (-300V)
   - **M100V**: Three readings at 100V

4. **Review the results:**
   - The application automatically calculates all correction factors
   - Check the **Ecart (%)** - it should be within ±2% for a PASS

5. **Save your measurement:**
   - Click "Save Measurement" to store it in the database
   - View it later in the History section

### Viewing History

- Click the "History" button in the navigation
- Filter by energy or search for specific measurements
- Export individual reports as PDF
- Export all data as CSV

### Adding Your Hospital Logo

1. Place your logo file in: `server/wwwroot/logos/`
2. Name it `logo.png` or `logo.jpg` (or use clinic-specific names)
3. The logo will automatically appear in PDF reports

## 🔧 Troubleshooting

### Application won't start

- **Check .NET installation**: Run `dotnet --version` (should show 8.0 or higher)
- **Check port availability**: Make sure port 5000 isn't being used by another application
- **Check permissions**: On Linux, make sure the user has read/write permissions

### Database errors

- The database is created automatically on first run
- If you see database errors, delete `server/trs398.db` and restart the application
- Make sure the application has write permissions in the `server/` folder

### Apache proxy not working

- **Check if the application is running**: `sudo systemctl status trs398`
- **Check Apache error logs**: `sudo tail -f /var/log/apache2/error.log`
- **Test the application directly**: `curl http://localhost:5000`
- **Check firewall**: Make sure port 80 (or your configured port) is open

### Can't access from other computers

- **Check firewall settings**: Allow incoming connections on port 80
- **Check Apache configuration**: Make sure it's listening on the correct interface
- **Try accessing by IP**: `http://server-ip-address`

## 📝 Notes

- The database file (`trs398.db`) contains all your measurements - make sure to back it up regularly
- The application runs on port 5000 by default, but you can change this in the startup command
- For production use, consider setting up HTTPS/SSL for secure connections
- All measurements are stored locally in SQLite - no cloud connection required

## 🆘 Need Help?

If you encounter any issues:

1. Check the application logs (in the terminal where it's running)
2. Check Apache error logs: `/var/log/apache2/error.log`
3. Verify all requirements are installed correctly
4. Make sure ports are not blocked by firewall

## 📄 License

This application is provided as-is for medical physics calibration purposes.

---

**Happy calibrating!** 🎉
