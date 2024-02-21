(function (document) {
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
})(document);

const targetedVersions = ['1.0.0.5', '1.0.0.4'];

const protocolPath = 'crun://';

randomString = (l) => {
	s = '';
	c = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
	for (i = 0; i < l; i++) {
		s += c[(Math.random() * c.length) | 0];
	}
	return s;
};

let token =
	localStorage.getItem('CRNTOKEN') ||
	localStorage.setItem('CRNTOKEN', randomString(8));

let CrunHelper = {
	installPage: function () {
		window.location.replace(
			'https://github.com/Mrgaton/CRUNInstaller/releases/latest'
		);
	},

	installed: function () {
		return isFontAvailable('crun-rfont');
	},

	runElement: function (element) {
		let runType = element.getAttribute('type');
		let args = [];

		args.push(runType);
		args.push(element.getAttribute('showWindow') ?? 'true');

		switch (runType) {
			case 'run':
				args.push(element.getAttribute('shellExecute') ?? 'true');
				args.push(element.getAttribute('fileName') ?? 'cmd.exe');
				args.push(element.getAttribute('arguments') ?? '');
				break;

			case 'cmd':
				args.push(element.getAttribute('closeOnEnd') ?? 'true');
				args.push(element.getAttribute('command') ?? 'cmd.exe');
				break;

			case 'ps1':
				args.push(element.getAttribute('UseShellExecute') ?? 'true');
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

		var iframe = document.createElement('iframe');
		iframe.style.display = 'none';
		document.body.appendChild(iframe);
		iframe.src = protocolPath + parseToURI(command);

		console.log(iframe.src);
	},

	runCmd: function (command, showWindow, autoClose, ...extraParams) {
		let internalArgs = [];

		internalArgs.push('cmd');

		internalArgs.push('run="' + command + '"');
		internalArgs.push('showWindow=' + parseBoolean(showWindow));
		internalArgs.push('autoClose=' + parseBoolean(autoClose));

		for (let i = 0; i < extraParams.length; i++) {
			let bool = typeof extraParams[i] === 'boolean';

			internalArgs.push(
				bool ? parseBoolean(extraParams[i]) : extraParams[i]
			);
		}

		this.run(...internalArgs);
	},

	runProcess: function (
		filePath,
		args,
		showWindow,
		shellExecute,
		...extraParams
	) {
		let internalArgs = [];

		internalArgs.push('run');

		internalArgs.push('run="' + filePath + '"');
		internalArgs.push('args="' + args + '"');
		internalArgs.push('showWindow=' + parseBoolean(showWindow));
		internalArgs.push('shellExecute=' + parseBoolean(shellExecute));

		for (let i = 0; i < extraParams.length; i++)
			internalArgs.push(extraParams[i]);

		this.run(...internalArgs);
	}
};

function parseBoolean(bool) {
	return bool ? '1' : '0';
}

function parseToURI(...data) {
	let input = Array.isArray(data[0]) ? data[0] : data;

	return input.map(encodeURIComponent).join('/');
}