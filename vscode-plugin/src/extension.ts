/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as path from 'path';
import { workspace, ExtensionContext, WorkspaceConfiguration, commands, window, OutputChannel, Uri } from 'vscode';
import {
	LanguageClient,
	LanguageClientOptions,
	ServerOptions,
	ErrorAction,
	CloseAction
} from 'vscode-languageclient/node';
import * as fs from 'fs';
import { ChildProcess, spawn } from 'child_process';
const serverPath = "out/server/GameDialog.Server.dll"

let client: LanguageClient;
let server: ChildProcess;

export function activate(context: ExtensionContext) {
	commands.registerCommand('extension.createConstants', createConstants);
	let configuration = workspace.getConfiguration("gamedialog");
	let enable = configuration.get("EnableLanguageServer");
	const outputChannel = window.createOutputChannel("Game Dialog");
	if (enable)
		runServer(context, configuration, outputChannel);
}

async function runServer(context: ExtensionContext, configuration: WorkspaceConfiguration, outputChannel: OutputChannel) {
	let serverOptions: ServerOptions = async (): Promise<ChildProcess> => {
		interface IDotnetAcquireResult { dotnetPath: string; }

		const dotnetAcquisition = await commands.executeCommand<IDotnetAcquireResult>(
			'dotnet.acquire',
			{
				version: '6.0',
				requestingExtensionId: 'game-dialog'
			}
		);
		const dotnetPath = dotnetAcquisition?.dotnetPath ?? null;
		if (!dotnetPath)
			throw new Error('Server launch failure: .NET not found.');
		const languageServerExe = dotnetPath;
		const fullServerPath = path.resolve(context.asAbsolutePath(serverPath));
		if (!fs.existsSync(fullServerPath))
			throw new Error(`Server launch failure: no file exists at ${serverPath}`);
		server = spawn(languageServerExe, [fullServerPath]);
		window.showInformationMessage(`Started server ${serverPath} - PID ${server.pid}`);
		return server;
	};
	let clientOptions: LanguageClientOptions = {
		initializationFailedHandler: (error) => {
            window.showErrorMessage("Language server initialization failed");
            return false;
        },
        errorHandler: {
            error(error, message, count) {
                return { action: ErrorAction.Continue };
            },
            closed: () => {
                return { action: CloseAction.DoNotRestart }
            }
        },
		outputChannel: outputChannel,
		traceOutputChannel: outputChannel,
		initializationOptions: [configuration],
		documentSelector: [{ scheme: 'file', language: 'gamedialog' }],
		synchronize: {
			configurationSection: 'gamedialog',
			fileEvents: [workspace.createFileSystemWatcher("**/*.dia")]
		}
	};

	client = new LanguageClient(
		'gamedialog',
		'Game Dialog',
		serverOptions,
		clientOptions,
		true
	);

	await client.start().catch(e => {
		outputChannel.appendLine(`Server failed. ${JSON.stringify(e)}`);
		window.showErrorMessage("Server failed to run.", "Show output").then(res => {
			if (res !== undefined)
				outputChannel.show(true);
		});
	});
}

async function stopServer(): Promise<void> {
    await client.stop();
    server.kill();
}

async function createConstants(uri: Uri) {
	try {
		let filePath = path.parse(uri.fsPath);
		let namespace = filePath.dir.replace(workspace.rootPath, '').replace(/\\/g, '.');
		namespace = namespace.substring(1, namespace.length);
		const data = await fs.promises.readFile(uri.fsPath, { encoding: 'utf-8' });
		const fields = data.split('\r\n').map((line, index) => {
			if (index === 0 || line === '')
				return;
			let key = line.substring(0, line.indexOf(','))
			return `\n    public const string ${key} = \"${key}\";`;
		}).join('');
		let str = `namespace ${namespace};`
			.concat(`\npublic static class ${filePath.name}`)
			.concat("\n{", fields, "\n}");
		await fs.promises.writeFile(`${filePath.dir}\\${filePath.name}.cs`, str);
	} catch (err) {
		console.log(err);
	}
}

export function deactivate(): Thenable<void> | undefined {
	if (!client)
		return undefined;
	return stopServer();
}
