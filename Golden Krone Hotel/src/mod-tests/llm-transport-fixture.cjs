// Exercise the production NW.js HTTP transport in Node, with a small engine fixture.
const fs = require('fs'), vm = require('vm'), path = require('path');
const root = path.resolve(__dirname, '..');
const unload = [];
const sandbox = {
    require, process: {env: {LOCALAPPDATA: process.argv[2]}, pid: process.pid},
    console, Buffer, setTimeout, clearTimeout,
    document: {querySelector: () => null, createElement: () => ({innerHTML: '', get textContent() {return this.innerHTML;}})},
    $: () => ({is: () => false}),
    game: {state: 'updateSplash', actionQueue: [], turnCount: 0, consoleMessages: []},
    map: {}, menu: {}, magic: {}, abilities: {}, options: {getValue: () => false},
    hints: {}, title: {}, player: {},
    input: {defaultKeys: {WAIT: {}}, queueCommand: function () {sandbox.game.turnCount++;}},
    addEventListener: (name, fn) => {if (name === 'beforeunload') unload.push(fn);}
};
sandbox.window = sandbox;
vm.createContext(sandbox);
vm.runInContext(fs.readFileSync(path.join(root, 'package.nw/js/llm-bridge.js'), 'utf8'), sandbox);
process.stdin.resume();
process.stdin.on('end', () => {unload.forEach(fn => fn());});
