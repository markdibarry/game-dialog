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
import * as os from 'os';
import * as fs from 'fs';
import { ChildProcess, spawn } from 'child_process';
const serverPath = "out/server/GameDialog.Server.dll";
const extensionId = "gamedialog";
const notificationMethod = 'dialog/recompileAllFiles';

interface NotificationRequest { }
interface NotificationResponse { data: Array<string>; }

let client: LanguageClient;
let server: ChildProcess;

export function activate(context: ExtensionContext) {
    commands.registerCommand('extension.createConstants', createConstants);
    commands.registerCommand('extension.recompileAllFiles', recompileAllDialogFiles);
    let configuration = workspace.getConfiguration(extensionId);
    let enable = configuration.get("enabled");
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
                version: '9.0',
                requestingExtensionId: extensionId
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
        documentSelector: [{ scheme: 'file', language: extensionId }],
        synchronize: {
            configurationSection: extensionId,
            fileEvents: [workspace.createFileSystemWatcher("**/*.dia")]
        }
    };

    client = new LanguageClient(
        extensionId,
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

async function recompileAllDialogFiles(): Promise<void> {
    const message = `This will recompile all dialog files in your workspace.`;
    const confirm = 'Proceed';
    const choice = await window.showWarningMessage(message, { modal: true }, confirm );

    if (choice !== confirm) {
        return;
    }

    try {
        const params: NotificationRequest = {};
        const result = await client.sendRequest<NotificationResponse>(notificationMethod, params);

        if (result.data.length > 0)
        {
            let errorText = result.data.join(os.EOL);
            window.showErrorMessage(`The following files had errors and could not be compiled:${os.EOL}${errorText}`);
        }
        else
        {
            window.showInformationMessage(`All files compiled successfully.`);
        }
    } catch (err) {
        console.error(err);
    }
}

/**
 * Reads a CSV file specified by the provided URI, extracts the translation keys,
 * and generates a C# static class containing constant string fields for each key. The generated
 * class is saved in the same directory as the CSV file.
 *
 * @param {Uri} uri - The URI of the CSV file from which to extract translation keys.
 * @throws {Error} Throws an error if the file cannot be read or written.
 * 
 * @returns {Promise<void>} A promise that resolves when the file has been successfully created.
 */
async function createConstants(uri: Uri): Promise<void> {
    try {
        let filePath = path.parse(uri.fsPath);
        let namespace = filePath.dir
            .replace(workspace.workspaceFolders[0].uri.fsPath, '')
            .replace(new RegExp(`\\${path.sep}`, 'g'), '.')
            .slice(1);
        const keys = (await fs.promises.readFile(uri.fsPath, { encoding: 'utf-8' }))
            .split(os.EOL)
            .slice(1, -1)
            .filter(line => line.trim() !== '')
            .map(x => x.substring(0, x.indexOf(',')));
        let fields = keys
            .map(key => `    public const string ${key} = "${key}";`)
            .join(os.EOL);
        let fileContent = [
            `namespace ${namespace};`,
            '',
            `public static class ${filePath.name}`,
            `{`,
            fields,
            `}`
        ].join(os.EOL);
        let newFileName = `${filePath.dir}${path.sep}${filePath.name}.cs`;
        await fs.promises.writeFile(newFileName, fileContent);
    } catch (err) {
        console.log(err);
    }
}

export function deactivate(): Thenable<void> | undefined {
    if (!client)
        return undefined;
    return stopServer();
}
