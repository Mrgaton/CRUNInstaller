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

# Default Options

### `showwindow` `boolean`: Hides or shows the file of the process
### `shellexecute` `boolean`: Turn on shell execute
### `uac` `boolean`: Request admin elevation to the file

### `cd` (string): Sets the current directory to the specified one, usefoul to specify where to download the files
### `run` (string): The file path or the url of the file or the command in case of (cmd | ps1 | eps1)
### `args` (string): The arguments of the file to run

### `files` (string): Files uris to download separated by char `|` wich the file name can be specified after a `^` example:

```
files="https://github.com/file.exe^f1.exe|https://github.com/otherFile.exe^f2.exe"
```


Examples:
# RUN
Runs the file based in the default options

# CMD
Runs a cmd based in the default options
### `autoclose` (boolean): Auto closes the cmd after it end whatever is running

# PS1
### `autoclose` (boolean): Auto closes the powershell after it end whatever is running

# EPS1
### `autoclose` (boolean): Auto closes the powershell after it end whatever is running

# ZIP
### `zip` (string): The link of the zip to be downloaded, can also changed the name of the folder based on `^` can be null to be downloaded on the current dir, by default is downloaded on the current dir plus the hash of the zip uri

# Examples
```
//Shutdown computer in 40 seconds
crun.exe cmd run="shutdown /s /t 40 /f"

//Hello world in Base64 encoded powershell command
crun.exe eps1 showwindow=1 autoclose=0 run="ZQBjAGgAbwAgACIASABlAGwAbABvACAAdwBvAHIAbABkACIA"

//Run sfc scannow
crun.exe run showwindow=1 shellexecute=1 uac=1 run="sfc" args="/ScanNow"
```

To use from a website

```js
var iframe = document.createElement("iframe");
iframe.style.display = "none";
document.body.appendChild(iframe);
iframe.src = "crun://run/true/true/cmd";
```
