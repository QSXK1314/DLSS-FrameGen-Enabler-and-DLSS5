$src = "D:\desktop\DLSS5+多帧生成软件项目\winforms版本\DLSS5+MFG Enable"
$dst = "D:\desktop\DLSS5+多帧生成软件项目\WinUI3最终发布版\DLSS5+MFG Enable"
$launcherSrc = "D:\desktop\DLSS5+多帧生成软件项目\winforms版本\DFGE.exe"
$launcherDst = "D:\desktop\DLSS5+多帧生成软件项目\WinUI3最终发布版\DFGE.exe"

New-Item -ItemType Directory -Path $dst -Force | Out-Null
Copy-Item -Path $src -Destination $dst -Recurse -Force
Copy-Item -Path $launcherSrc -Destination $launcherDst -Force

Write-Host "复制完成！"
Get-ChildItem "D:\desktop\DLSS5+多帧生成软件项目\WinUI3最终发布版" | Select-Object Name
