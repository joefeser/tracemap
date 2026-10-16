@echo off
For /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set start_time=%%a:%%b)

mstest.exe /noresults /testsettings:src/local.testsettings /testcontainer:src/bin/debug/netperf.common.tests.dll /testcontainer:src/bin/debug/netperf.tests.dll /testcontainer:src/bin/debug/netperf.ui.tests.unit.dll /testcontainer:src/bin/debug/netperf.core.tests.unit.dll /testcontainer:src/bin/debug/netperf.tests.integration.dll

For /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set end_time=%%a:%%b)

echo
echo
echo tests ran from %start_time% to %end_time%

