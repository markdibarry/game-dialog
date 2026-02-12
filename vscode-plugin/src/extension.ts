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

interface GenerateTranslationRequest { isCSV: boolean }
interface GenerateTranslationResponse { data: Array<string>; }
interface UpdateMembersRequest { }
interface UpdateMembersResponse { }

let client: LanguageClient;
let server: ChildProcess;

export function activate(context: ExtensionContext) {
    commands.registerCommand('extension.generateCSV', () => generateTranslation(true));
    commands.registerCommand('extension.generatePOT', () => generateTranslation(false));
    commands.registerCommand('extension.updateMembers', () => updateMembers());

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
                version: '10.0',
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
        window.showInformationMessage(`Started GameDialog server - PID ${server.pid}`);
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

async function generateTranslation(isCSV: boolean): Promise<void> {
    let type = isCSV ? 'CSV' : 'POT';
    const message = `This will generate a ${type} translation file for all of your .dia files.`;
    const confirm = 'Proceed';
    const choice = await window.showWarningMessage(message, { modal: true }, confirm );

    if (choice !== confirm) {
        return;
    }

    try {
        const params: GenerateTranslationRequest = { isCSV };
        const result = await client.sendRequest<GenerateTranslationResponse>(`dialog/generateTranslation`, params);

        if (result.data.length > 0) {
            let errorText = result.data.join(os.EOL);
            window.showErrorMessage(`The following files had errors and need resolved:${os.EOL}${errorText}`);
        } else {
            window.showInformationMessage(`${type} file generated successfully.`);
        }
    } catch (err) {
        console.error(err);
    }
}

async function updateMembers(): Promise<void> {
    try {
        const params: UpdateMembersRequest = {};
        await client.sendRequest<UpdateMembersResponse>(`dialog/updateMembers`, params);
        window.showInformationMessage(`Dialog members updated successfully.`);
    } catch (err) {
        console.error(err);
        window.showErrorMessage(`The dialog members could not be updated. Please check for problems like duplicate members.`);
    }
}

export function deactivate(): Thenable<void> | undefined {
    if (!client)
        return undefined;
    return stopServer();
}
