/* ============================================================
   TRS-398 Pro — shared sidebar
   Single source of truth for the left navigation so every page
   renders an identical sidebar (same logo, icon sizes, items).
   Drop-in: <script src="sidebar.js"></script> before </body>.

   It replaces the innerHTML of the existing <nav class="sidebar">,
   so per-page CSS still applies but the content is uniform.
   Action items (Settings / Export / Reports) call the page's own
   handler ONLY if it exists, otherwise they navigate — never throw.
   ============================================================ */
(function () {
  "use strict";

  // canonical navigation — edit here once, applies everywhere
  var WORKSPACE = [
    { href: "dashboard.html", icon: "layout-dashboard", label: "Dashboard" },
    { href: "index.html",     icon: "circle-dot",       label: "Calibration" },
    { href: "history.html",   icon: "history",          label: "History" },
    { href: "control.html",   icon: "activity",         label: "Control Charts" },
    { href: "linacs.html",    icon: "cpu",              label: "Machines" },
    { href: "calendar.html",  icon: "calendar",         label: "QA Calendar" },
    { href: "detectors.html", icon: "zap",              label: "Detectors" }
  ];

  // footer actions — call handler if present on this page, else fall back
  var SYSTEM = [
    { icon: "settings",     label: "Settings",        fn: "openSetup",          fallback: "dashboard.html" },
    { icon: "database",     label: "Backup",          href: "backup.html" },
    { icon: "download",     label: "Export CSV",      fn: "downloadCSV",        fallback: "dashboard.html" },
    { icon: "file-archive", label: "All Reports PDF", fn: "downloadAllPdfZip",  fallback: "dashboard.html" }
  ];

  var LOGO_SVG =
    '<svg class="logo-icon" viewBox="0 0 100 100" fill="none" xmlns="http://www.w3.org/2000/svg">' +
    '<circle cx="50" cy="50" r="46" stroke="currentColor" stroke-width="4"/>' +
    '<circle cx="50" cy="50" r="8" fill="currentColor"/>' +
    '<line x1="50" y1="4" x2="50" y2="24" stroke="currentColor" stroke-width="4" stroke-linecap="round"/>' +
    '<line x1="50" y1="76" x2="50" y2="96" stroke="currentColor" stroke-width="4" stroke-linecap="round"/>' +
    '<line x1="4" y1="50" x2="24" y2="50" stroke="currentColor" stroke-width="4" stroke-linecap="round"/>' +
    '<line x1="76" y1="50" x2="96" y2="50" stroke="currentColor" stroke-width="4" stroke-linecap="round"/>' +
    '<line x1="18" y1="18" x2="32" y2="32" stroke="currentColor" stroke-width="3" stroke-linecap="round" opacity="0.5"/>' +
    '<line x1="68" y1="68" x2="82" y2="82" stroke="currentColor" stroke-width="3" stroke-linecap="round" opacity="0.5"/>' +
    '<line x1="82" y1="18" x2="68" y2="32" stroke="currentColor" stroke-width="3" stroke-linecap="round" opacity="0.5"/>' +
    '<line x1="32" y1="68" x2="18" y2="82" stroke="currentColor" stroke-width="3" stroke-linecap="round" opacity="0.5"/>' +
    '</svg>';

  function currentPage() {
    var p = (location.pathname.split("/").pop() || "index.html").toLowerCase();
    return p === "" ? "index.html" : p;
  }

  function esc(s) { return String(s).replace(/"/g, "&quot;"); }

  function build() {
    var here = currentPage();
    var html = '' +
      '<div class="sidebar-logo">' + LOGO_SVG +
        '<div><div class="logo-name">TRS-398 <span>Pro</span></div>' +
        '<div class="logo-version">v2.1</div></div>' +
      '</div>' +
      '<div class="sidebar-nav"><span class="sidebar-label">Workspace</span>';

    WORKSPACE.forEach(function (it) {
      var active = (it.href.toLowerCase() === here) ? " active" : "";
      html += '<a href="' + esc(it.href) + '" class="sidebar-nav-item' + active + '">' +
              '<i data-lucide="' + esc(it.icon) + '"></i> ' + esc(it.label) + '</a>';
    });

    html += '</div><div class="sidebar-footer"><span class="sidebar-label">System</span>';
    SYSTEM.forEach(function (it, i) {
      if (it.href) {
        html += '<button class="sidebar-nav-item" data-nav="' + esc(it.href) + '">' +
                '<i data-lucide="' + esc(it.icon) + '"></i> ' + esc(it.label) + '</button>';
      } else {
        html += '<button class="sidebar-nav-item" data-sys="' + i + '">' +
                '<i data-lucide="' + esc(it.icon) + '"></i> ' + esc(it.label) + '</button>';
      }
    });
    html += '</div>';
    return html;
  }

  function wire(nav) {
    nav.querySelectorAll("[data-nav]").forEach(function (b) {
      b.addEventListener("click", function () { location.href = b.getAttribute("data-nav"); });
    });
    nav.querySelectorAll("[data-sys]").forEach(function (b) {
      b.addEventListener("click", function () {
        var item = SYSTEM[+b.getAttribute("data-sys")];
        var handler = window[item.fn];
        if (typeof handler === "function") {
          try { handler(); }
          catch (e) { console.error("[sidebar] " + item.fn + " failed:", e); location.href = item.fallback; }
        } else {
          location.href = item.fallback;
        }
      });
    });
  }

  function refreshIcons() {
    if (window.lucide && typeof window.lucide.createIcons === "function") {
      try { window.lucide.createIcons(); } catch (e) { /* ignore */ }
      return true;
    }
    return false;
  }

  function render() {
    var nav = document.querySelector("nav.sidebar, .sidebar");
    if (!nav) return;                 // pages without a sidebar (e.g. activate) are skipped
    nav.innerHTML = build();
    wire(nav);
    // Lucide may load after us — retry briefly until icons render.
    if (!refreshIcons()) {
      var tries = 0, t = setInterval(function () {
        if (refreshIcons() || ++tries > 20) clearInterval(t);
      }, 100);
    }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", render);
  } else {
    render();
  }
})();
