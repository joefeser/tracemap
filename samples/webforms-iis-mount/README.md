# Master-page script under an IIS application mount

Scan this directory as the repository. Give the scanner
`--webforms-iis-mount '/Portal/Desk=Site'`. The script include in
`Site/Layout.master` line 4 resolves to `Site/Scripts/panel.js` in the
inventory. The click on `Site/Page.aspx` line 3 links to the assignment
on line 2 of that script. Without the mapping it has a typed script-path gap.
Use `.` for the mapped directory when the scan root is the web root itself.
