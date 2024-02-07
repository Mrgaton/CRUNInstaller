[![Build status](https://ci.appveyor.com/api/projects/status/7ct5b4uk3mrr2oc4?svg=true)](https://ci.appveyor.com/project/Mrgaton/CRUNInstaller)
[![CodeFactor][img_codefactor]][codefactor]
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/fe6f2024150c4d9492076a4da1a6ccfa)](https://app.codacy.com/gh/Mrgaton/CRUNInstaller)
[![MIT License][img_license]][license]
![visitors](https://visitor-badge.laobi.icu/badge?page_id=Mrgaton.CRUNInstaller)
[![Downloads](https://img.shields.io/github/downloads/Mrgaton/CRUNInstaller/total?color=green)]()

[codefactor]: https://www.codefactor.io/repository/github/Mrgaton/CRUNInstaller/overview
[license]: LICENSE.md
[img_build]: https://img.shields.io/appveyor/ci/Mrgaton/CRUNInstaller/master.svg?style=flat
[img_codefactor]: https://www.codefactor.io/repository/github/mrgaton/CRUNInstaller/badge
[img_license]: https://img.shields.io/github/license/Mrgaton/CRUNInstaller.svg?style=flat

# CRUNInstaller

A crappy project that I am experimenting to be able to interact with the computer from web pages

Examples:

```cs
//For running normal file:

crun.exe run run="somefile.exe" args="/someAction" shellExecute=true showWindow=false requestUac=true

//Running from url

crun.exe cmd run="https://example.com/file.exe" args="/someAction" shellExecute=true showWindow=false requestUac=true

//Running a cmd command

crun.exe cmd run="echo buenos dias" shellExecute=true showWindow=true requestUac=true autoClose=false

//Running powershell commands

crun.exe ps1 run="Get-Help" shellExecute=true showWindow=true requestUac=true autoClose=false
```

To use from a website

```js
var iframe = document.createElement("iframe");
iframe.style.display = "none";
document.body.appendChild(iframe);
iframe.src = "crun://run/run="sfc.exe"/args="/scannow"/cmd";
```
