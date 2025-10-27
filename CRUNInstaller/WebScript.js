var CrunHelper = null;
var CrunServer = null;

// Por fin actualizo el crun.js que ya estaba un poco chungo.

(() => {
	let width;
	let body = document.body;

	let container = document.createElement('span');
	container.innerHTML = Array(100).join('wi');
	container.style.cssText = [
		'position:absolute',
		'width:auto',
		'font-size:128px',
		'left:-99999px'
	].join(' !important;');

	const getWidth = function (fontFamily) {
		container.style.fontFamily = fontFamily;

		body.appendChild(container);
		width = container.clientWidth;
		body.removeChild(container);

		return width;
	};

	const monoWidth = getWidth('monospace');
	const serifWidth = getWidth('serif');
	const sansWidth = getWidth('sans-serif');

	window.isFontAvailable = function (font) {
		return (
			monoWidth !== getWidth(font + ',monospace') ||
			sansWidth !== getWidth(font + ',sans-serif') ||
			serifWidth !== getWidth(font + ',serif')
		);
	};

	const targetedVersions = ['1.7.1.0'];

	const protocolPath = 'crun://';

	function randomString(length) {
		let s = '';
		const chars =
			'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
		for (let i = 0; i < length; i++) {
			s += chars[(Math.random() * chars.length) | 0];
		}

		return s;
	}

	let tokenKey = 'CRNTOKEN';

	const tokenSize = 32;

	let token = this.localStorage.getItem(tokenKey);

	if (!token || token.length < 32) {
		token = randomString(tokenSize);

		this.localStorage.setItem(tokenKey, token);
	}

	CrunHelper = {
		installPage: function () {
			window.location.replace(
				'https://github.com/Mrgaton/CRUNInstaller/releases/latest'
			);
		},

		installed: function () {
			if (navigator.brave && navigator.brave.isBrave()) return true;

			return window.isFontAvailable('crun-rfont');
		},

		runElement: function (element) {
			let runType = element.getAttribute('type');
			let args = [];

			args.push(runType);

			switch (runType) {
				case 'run':
					args.push(element.getAttribute('fileName') ?? 'cmd.exe');
					args.push(
						'args=' + element.getAttribute('arguments') ?? ''
					);
					break;

				case 'cmd':
					args.push(element.getAttribute('command') ?? 'cmd.exe');
					args.push(
						'autoclose=' + element.getAttribute('autoclose') ??
						'true'
					);
					break;

				case 'zip':
					args.push(
						element.getAttribute('fileName') ?? 'example.exe'
					);
					args.push('zip=' + element.getAttribute('zip'));
					args.push(
						'autoclose=' + element.getAttribute('autoclose') ??
						'true'
					);
					break;

				case 'ps1':
					args.push(element.getAttribute('command') ?? 'echo hola');
					args.push(
						'autoclose=' + element.getAttribute('autoclose') ??
						'true'
					);
					break;

				case 'eps1':
					args.push(element.getAttribute('command') ?? 'echo hola');
					args.push(
						'autoclose=' + element.getAttribute('autoclose') ??
						'true'
					);
					break;
			}

			args.push('shell=' + element.getAttribute('shell') ?? 'true');
			args.push('hide=' + element.getAttribute('hide') ?? 'false');
			args.push('uac=' + element.getAttribute('uac') ?? 'false');

			console.log(...args);

			this.runCore(...args);
		},

		runCore: function (...command) {
			command.push('tarjetVersion="' + targetedVersions.join(',') + '"');
			command.push('cname=' + window.location.hostname);
			command.push('ctoken=' + token);

			let iframe = document.createElement('iframe');
			iframe.style.display = 'none';
			document.body.appendChild(iframe);
			iframe.src = protocolPath + parseToURI(command);

			console.debug('CRUN: ' + iframe.src);
		},

		run: function (
			command,
			args,
			hide,
			shellExecute = false,
			uac = false,
			...extraParams
		) {
			let internalArgs = [];

			internalArgs.push('run');

			internalArgs.push(command);
			internalArgs.push('args=' + (args || ''));
			internalArgs.push('uac=' + parseToBool(uac));
			internalArgs.push('shell=' + parseToBool(shellExecute));
			internalArgs.push('hide=' + parseToBool(hide));

			for (let i = 0; i < extraParams.length; i++) {
				let bool = typeof extraParams[i] === 'boolean';

				internalArgs.push(
					bool ? parseToBool(extraParams[i]) : extraParams[i]
				);
			}

			this.runCore(...internalArgs);
		},

		runPs1: function (
			command,
			autoClose = false,
			hide = false,
			...extraParams
		) {
			let internalArgs = [];

			internalArgs.push('ps1');

			internalArgs.push(command);
			internalArgs.push('hide=' + parseToBool(hide));
			internalArgs.push('autoClose=' + parseToBool(autoClose));

			for (let i = 0; i < extraParams.length; i++) {
				let bool = typeof extraParams[i] === 'boolean';

				internalArgs.push(
					bool ? parseToBool(extraParams[i]) : extraParams[i]
				);
			}

			this.runCore(...internalArgs);
		},

		runCmd: function (
			command,
			autoClose = false,
			hide = false,
			...extraParams
		) {
			let internalArgs = [];

			internalArgs.push('cmd');

			internalArgs.push(command);
			internalArgs.push('hide=' + parseToBool(hide));
			internalArgs.push('autoClose=' + parseToBool(autoClose));

			for (let i = 0; i < extraParams.length; i++) {
				let bool = typeof extraParams[i] === 'boolean';

				internalArgs.push(
					bool ? parseToBool(extraParams[i]) : extraParams[i]
				);
			}

			this.runCore(...internalArgs);
		}
	};

	/*if (navigator.brave && navigator.brave.isBrave()) {
	CrunHelper = null;

	alert('Brave is not supported, try ussing edge or crome 🤗');

	window.location.replace('https://www.google.com/intl/es_es/chrome/');
}*/

	function parseToBool(bool) {
		return bool ? '1' : '0';
	}

	function parseFromBool(str) {
		return (
			str === '1' ||
			str === 'true' ||
			str === 'yes' ||
			str === 'y' ||
			str === 'ok'
		);
	}

	function cleanPath(path) {
		return path.replace(/\//g, '\\').replace(/\\\\/g, '\\');
	}

	function parseToURI(...data) {
		let input = Array.isArray(data[0]) ? data[0] : data;

		return input.map(encodeURIComponent).join('/');
	}

	const uri = 'http://127.0.0.1:51213';

	const cfetch = async (data, options = {}) => {
		await healthCheck();

		if (!options.headers) options.headers = {};
		options.headers.authorization = token;

		const res = await fetch(`${uri}/${data}`, options);

		const text = await res.text();

		if (res.status > 300) {
			throw new Error(text);
		}

		return text;
	};

	let healthInterval;

	const healthCheck = async (timeout = 600, regularCheck = true) => {
		try {
			const response = await fetch(uri + '/health', {
				headers: {
					authorization: token
				},
				signal: AbortSignal.timeout(timeout),
				method: 'GET',
				priority: 'high'
			});

			console.log(
				'[CrunServer] Sending heartbeat: ' + (await response.status)
			);

			return response.status;
		} catch (error) {
			console.error(error);

			if (regularCheck) {
				await new Promise((r) => setTimeout(r, 100));

				CrunServer.checkAndStart();
			}

			return 0;
		}
	};

	document.addEventListener('click', function (event) {
		if (
			event.target.tagName === 'BUTTON' ||
			event.type.toUpperCase() === 'BUTTON'
		) {
			handleButtonClick(event.target);
		}
	});

	function handleButtonClick(button) {
		const crunAttr = button.getAttribute('crun');

		if (!crunAttr) return;

		const sepIndex = crunAttr.indexOf(';');
		if (sepIndex === -1) {
			return;
		}

		const methodPathRaw = crunAttr.slice(0, sepIndex);
		const argsPart = crunAttr.slice(sepIndex + 1);

		const methodPath = methodPathRaw.trim();
		const argsArray = parseArguments(argsPart);

		// Retrieve the method function
		const methodFunc = getMethodByPath(CrunServer, methodPath);

		if (typeof methodFunc === 'function') {
			methodFunc(...argsArray);
		} else {
			console.warn(`Method '${methodPath}' not found on CrunServer.`);
		}
	}

	function getMethodByPath(obj, path) {
		const parts = path.split('.');
		let current = obj;

		for (const part of parts) {
			if (current && typeof current === 'object') {
				const keys = Object.keys(current);
				const matchedKey = keys.find(
					(key) => key.toLowerCase() === part.toLowerCase()
				);
				if (matchedKey) {
					current = current[matchedKey];
				} else {
					return undefined;
				}
			} else {
				return undefined;
			}
		}

		return current;
	}

	function parseArguments(argsString) {
		return argsString.split(',').map((arg) => {
			const trimmed = arg.trim();
			const lowercased = trimmed.toLowerCase();

			if (lowercased === '') return '';
			if (lowercased === 'true') return true;
			if (lowercased === 'false') return false;
			if (!isNaN(trimmed)) return Number(trimmed);
			return encodeURIComponent(trimmed);
		});
	}

	let lastRun = 0; // Initialize last run timestamp

	CrunServer = {
		checkAndStart: async function () {
			let healthy = false;

			if ((await healthCheck(600, false)) == 200) {
				healthy = true;
			}

			const now = Date.now();

			if (!healthy && now - lastRun >= 10 * 1000) {
				CrunHelper.runCore('server');
				lastRun = now; // Update last run time
				setTimeout(healthCheck, 1 * 1000);
			}

			if (!healthInterval) {
				healthInterval = setInterval(healthCheck, 7 * 1000);
			}

			return healthy;
		},

		stop: function () {
			CrunHelper.runCore('stop');
			clearInterval(healthInterval);
			healthInterval = null;
		},

		runAsync: async function (
			file,
			args = '',
			hide = false,
			shell = true,
			uac = false
		) {
			return await CrunServer.run(file, args, hide, shell, uac);
		},

		run: async function (
			file,
			args = '',
			hide = false,
			shell = true,
			uac = false,
			async = false
		) {
			if (uac && !shell) {
				throw new Error(
					'Shell must be enabled when elevating uac privileges.'
				);
			}

			return await cfetch(
				'run?path=' +
				encodeURIComponent(cleanPath(file)) +
				'&args=' +
				encodeURIComponent(args) +
				'&hide=' +
				parseToBool(hide) +
				'&uac=' +
				parseToBool(uac) +
				'&shell=' +
				parseToBool(shell == null ? true : shell) +
				'&async=' +
				parseToBool(async)
			);
		},

		runPowershell: async function (
			command,
			autoclose = true,
			hide = false,
			uac = false,
			shell = true
		) {
			return await CrunServer.run(
				'%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe',
				'-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass' +
				(!autoclose ? ' -NoExit' : null) +
				' -Command "& "' +
				command +
				'""',
				hide,
				shell,
				uac
			);
		},

		files: {
			write: async function (path, content) {
				if (!path) path = '.';

				const res = await cfetch(
					'write?path=' + encodeURIComponent(path),
					{
						method: 'POST',
						body: content
					}
				);

				let out = res.trim().toLocaleLowerCase();

				return parseFromBool(out);
			},

			exist: async function (path) {
				if (!path) path = '.';

				const res = await cfetch(
					'exist?path=' + encodeURIComponent(path)
				);

				let out = res.trim().toLocaleLowerCase();

				return parseFromBool(out);
			},

			list: async function (path, pattern = '') {
				if (!path) path = '.';

				const res = await cfetch(
					'list?path=' +
					encodeURIComponent(path) +
					'&pattern=' +
					pattern
				);

				let obj = [];

				res.split('\n').forEach((element) => {
					obj.push(element);
				});

				return obj;
			},

			read: async function (path) {
				const res = await cfetch(
					'read?path=' + encodeURIComponent(path) + '&base64=true'
				);

				return atob(res);
			},

			move: async function (oldPath, newPath) {
				const res = await cfetch(
					'move?path=' +
					encodeURIComponent(oldPath) +
					'&new=' +
					encodeURIComponent(newPath)
				);

				return atob(res);
			},

			download: async function (url, path) {
				return await cfetch(
					'download?url=' +
					encodeURIComponent(url) +
					'&path=' +
					encodeURIComponent(path)
				);
			},

			delete: async function (path) {
				return await cfetch('delete?path=' + encodeURIComponent(path));
			},

			attributes: async function (path) {
				return await cfetch(
					'attributes?path=' + encodeURIComponent(path)
				);
			}
		},

		directory: {
			delete: async function (path, recursive = true) {
				return await cfetch(
					'delete?path=' +
					encodeURIComponent(path) +
					'&recursive=' +
					recursive
				);
			},

			list: async function (path, pattern) {
				return await CrunServer.files.list(path);
			},

			exist: async function (path) {
				return await CrunServer.files.exist(path.trim('/') + '/');
			},

			getCurrentDirectory: async function () {
				return await cfetch('gcd');
			},

			setCurrentDirectory: async function (path) {
				return (
					(await cfetch('scd?path=' + encodeURIComponent(path))) ===
					''
				);
			}
		},

		services: {
			start: async function (name, ...args) {
				return await cfetch(
					'service/start?path=' +
					name +
					'&args=' +
					args
						.map(function (a) {
							return encodeURIComponent(a);
						})
						.join('|')
				);
			},
			stop: async function (name) {
				return await cfetch('service/stop?path=' + name);
			},

			restart: async function (name, ...args) {
				return await cfetch(
					'service/restart?path=' +
					name +
					'&args=' +
					args
						.map(function (a) {
							return encodeURIComponent(a);
						})
						.join('|')
				);
			},

			info: async function (name) {
				const info = (await cfetch('service/info?path=' + name)).split(
					'|'
				);

				return {
					name: info[0],
					type: info[1],
					start: info[2],
					status: info[3]
				};
			},

			list: async function () {
				let list = [];

				(await cfetch('service/list'))
					.split('\n')
					.forEach((element) => {
						const info = element.split('|');

						list.push({
							name: info[0],
							type: info[1],
							start: info[2],
							status: info[3]
						});
					});

				return list;
			}
		},

		registry: {
			get: async function (path, key) {
				return await cfetch(
					'registry/get?path=' +
					encodeURIComponent(path) +
					'&key=' +
					key
				);
			},

			set: async function (path, key, value, kind) {
				return await cfetch(
					'registry/set?path=' +
					path +
					'&key=' +
					key +
					'&value=' +
					encodeURIComponent(value) +
					'&kind=' +
					kind
				);
			},

			delete: async function (path, key) {
				return await cfetch(
					'registry/delete?path=' +
					encodeURIComponent(path) +
					'&key=' +
					key
				);
			},

			list: async function (path) {
				return await cfetch(
					'registry/list?path=' + encodeURIComponent(path)
				);
			}
		},

		managementSearch: async function (path) {
			const res = await cfetch(
				'management/query?path=' + encodeURIComponent(path)
			);

			let obj = {};
			res.split('\n').forEach((element) => {
				let split = element.split('|');

				obj[split[0]] = Number(split[1]);
			});
			return obj;
		},

		dllInvoke: async function (dll, method, returnType, params) {
			const res = await cfetch(
				'dllinvoke?dll=' +
				encodeURIComponent(dll) +
				'&method=' +
				encodeURIComponent(method) +
				'&params=' +
				encodeURIComponent(params) +
				'&returnType=' +
				returnType ?? 'void'
			);

			return res;
		},

		processList: async function () {
			let obj = {};

			(await cfetch('plist')).split('\n').forEach((element) => {
				let split = element.split('|');

				obj[split[0]] = Number(split[1]);
			});

			return obj;
		},

		getEnv: async function () {
			let response = (await cfetch('env')).split('\n');
			let env = {};

			for (let line of response) {
				if (!line.trim() || line.trim().startsWith('#')) continue;

				let index = line.indexOf('=');
				if (index === -1) continue;

				let key = line.slice(0, index).trim();
				let value = line.slice(index + 1).trim();
				env[key] = value;
			}

			return env;
		},

		killProcess: async function (...processNames) {
			return Number(await cfetch('pkill?name=' + processNames.join('|')));
		},

		killProcessById: async function (pid) {
			return Number(await cfetch('pkill?pid=' + pid));
		},

		extractZip: async function (url, path) {
			return await cfetch(
				'unzip?url=' +
				encodeURIComponent(url) +
				'&path=' +
				encodeURIComponent(path)
			);
		}
	};
})();


window.CrunHelper = CrunHelper;
window.CrunServer = CrunServer;