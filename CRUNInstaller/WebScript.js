var CrunHelper = null;
var CrunServer = null;

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

	const targetedVersions = ['1.0.0.5', '1.6.0.0'];

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

	const token =
		this.localStorage.getItem(tokenKey) ||
		this.localStorage.setItem(tokenKey, randomString(10));

	const cfetch = async (data) => {
		await healthCheck();

		const res = await fetch(encodeURI(uri + '/' + data));

		return await res.text();
	};

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
			args.push(element.getAttribute('hide') ?? 'false');

			switch (runType) {
				case 'run':
					args.push(element.getAttribute('shell') ?? 'true');
					args.push(element.getAttribute('fileName') ?? 'cmd.exe');
					args.push(element.getAttribute('arguments') ?? '');
					break;

				case 'cmd':
					args.push(element.getAttribute('closeOnEnd') ?? 'true');
					args.push(element.getAttribute('command') ?? 'cmd.exe');
					break;

				case 'ps1':
					args.push(
						element.getAttribute('UseShellExecute') ?? 'true'
					);
					args.push(element.getAttribute('command') ?? 'echo hola');
					break;
			}

			console.log(...args);

			this.run(...args);
		},

		run: function (...command) {
			command.push('tarjetVersion="' + targetedVersions.join(',') + '"');
			command.push('cname=' + window.location.hostname);
			command.push('ctoken=' + token);

			let iframe = document.createElement('iframe');
			iframe.style.display = 'none';
			document.body.appendChild(iframe);
			iframe.src = protocolPath + parseToURI(command);

			console.debug('CRUN: ' + iframe.src);
		},

		runPs1: function (command, hide, autoClose, ...extraParams) {
			let internalArgs = [];

			internalArgs.push('ps1');

			internalArgs.push('run="' + command + '"');
			internalArgs.push('hide=' + parseBoolean(hide));
			internalArgs.push('autoClose=' + parseBoolean(autoClose));

			for (let i = 0; i < extraParams.length; i++) {
				let bool = typeof extraParams[i] === 'boolean';

				internalArgs.push(
					bool ? parseBoolean(extraParams[i]) : extraParams[i]
				);
			}

			this.run(...internalArgs);
		},

		runCmd: function (command, hide = true, autoClose, ...extraParams) {
			let internalArgs = [];

			internalArgs.push('cmd');

			internalArgs.push('run="' + command + '"');
			internalArgs.push('hide=' + parseBoolean(hide));
			internalArgs.push('autoClose=' + parseBoolean(autoClose));

			for (let i = 0; i < extraParams.length; i++) {
				let bool = typeof extraParams[i] === 'boolean';

				internalArgs.push(
					bool ? parseBoolean(extraParams[i]) : extraParams[i]
				);
			}

			this.run(...internalArgs);
		},

		runProcess: function (filePath, args, hide, shell, ...extraParams) {
			let internalArgs = [];

			internalArgs.push('run');

			internalArgs.push('run="' + (filePath || '') + '"');

			if (args) internalArgs.push('args="' + (args || '') + '"');

			internalArgs.push('hide=' + parseBoolean(hide));
			internalArgs.push('shell=' + parseBoolean(shell));

			for (let i = 0; i < extraParams.length; i++)
				internalArgs.push(extraParams[i]);

			this.run(...internalArgs);
		}
	};

	/*if (navigator.brave && navigator.brave.isBrave()) {
	CrunHelper = null;

	alert('Brave is not supported, try ussing edge or crome 🤗');

	window.location.replace('https://www.google.com/intl/es_es/chrome/');
}*/

	function parseBoolean(bool) {
		return bool ? '1' : '0';
	}

	function cleanPath(path) {
		return path.replace(/\//g, '\\').replace(/\\\\/g, '\\');
	}

	function parseToURI(...data) {
		let input = Array.isArray(data[0]) ? data[0] : data;

		return input.map(encodeURIComponent).join('/');
	}

	const uri = 'http://127.0.0.1:51213';

	let healthInterval;

	const healthCheck = async (timeout = 600, regularCheck = true) => {
		try {
			const response = await fetch(uri + '/health', {
				signal: AbortSignal.timeout(timeout)
			});

			console.log(
				'[CrunServer] Sending heartbeat: ' + (await response.text())
			);

			return response.status;
		} catch (error) {
			console.error(error);

			if (regularCheck) {
				await new Promise((r) => setTimeout(r, 200));

				CrunServer.checkAndStart();
			}

			return 0;
		}
	};

	let lastRun = 0; // Initialize last run timestamp

	CrunServer = {
		checkAndStart: async function () {
			let healthy = false;

			if ((await healthCheck(600, false)) == 200) {
				healthy = true;
			}

			const now = Date.now();

			if (!healthy && now - lastRun >= 10000) {
				CrunHelper.run('server');
				lastRun = now; // Update last run time
				setTimeout(healthCheck, 1 * 1000);
			}

			if (!healthInterval) {
				healthInterval = setInterval(healthCheck, 7 * 1000);
			}

			return healthy;
		},

		stop: function () {
			CrunHelper.run('stop');
			clearInterval(healthInterval);
			healthInterval = null;
		},

		runAsync: async function (file, args, hide, admin, shell) {
			return await this.run(file, args, hide, admin, shell, true);
		},

		run: async function (file, args, hide, admin, shell = true, async) {
			return await cfetch(
				'run?file=' +
					cleanPath(file) +
					'&args=' +
					args +
					'&hide=' +
					parseBoolean(hide) +
					'&admin=' +
					parseBoolean(admin) +
					'&shell=' +
					parseBoolean(shell == null ? true : shell) +
					'&async=' +
					parseBoolean(async)
			);
		},

		files: {
			write: async function (path, content) {
				if (!path) path = '.';

				const res = await cfetch('write?path=' + path, {
					method: 'POST',
					body: content
				});

				let out = res.trim().toLocaleLowerCase();

				return out === '1' || out === 'true';
			},

			exist: async function (path) {
				if (!path) path = '.';

				const res = await cfetch('exist?path=' + path);

				let out = res.trim().toLocaleLowerCase();

				return out === '1' || out === 'true';
			},

			list: async function (path) {
				if (!path) path = '.';

				const res = await cfetch('list?path=' + path);

				let obj = [];

				res.split('\n').forEach((element) => {
					obj.push(element);
				});

				return obj;
			},

			read: async function (path) {
				const res = await cfetch('read?path=' + path + '&base64=true');

				return atob(res);
			},

			delete: async function (path) {
				return await cfetch('delete?path=' + path);
			},

			attributes: async function (path) {
				return await cfetch('attributes?path=' + path);
			}
		},

		directory: {
			delete: async function (path, recursive = true) {
				return await cfetch(
					'delete?path=' + path + '&recursive=' + recursive
				);
			},

			list: async function (path) {
				return await CrunServer.files.list(path);
			},

			getCurrentDirectory: async function () {
				return await cfetch('gcd');
			},

			setCurrentDirectory: async function (path) {
				return await cfetch('sgd?path=' + path);
			}
		},

		processList: async function () {
			const res = await cfetch('plist');

			let obj = {};
			res.split('\n').forEach((element) => {
				let split = element.split(':');

				obj[split[0]] = Number(split[1]);
			});
			return obj;
		},

		killProcess: async function (...processNames) {
			return await cfetch('pkill?name=' + processNames.join('|'));
		},

		extractZip: async function (url, path) {
			return await cfetch('extract?url=' + url + '&path=' + path);
		}
	};
})();
