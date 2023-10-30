# CRUNInstaller

A crappy project that I am experimenting to be able to interact with the computer from web pages

Examples:

```
crun.exe run [ShowWindow] [UseShellExecute] [FileName] [Arguments]"
crun.exe cmd [ShowWindow] [CloseOnEnd] [Command\\Batch URI]
crun.exe ps1 [ShowWindow] [UseShellExecute] [Command\\Powershell Script URI]
```
To use from a website

```js
var iframe = document.createElement('iframe');
iframe.style.display = 'none';
document.body.appendChild(iframe);
iframe.src = 'crun://run/true/true/cmd';
```
