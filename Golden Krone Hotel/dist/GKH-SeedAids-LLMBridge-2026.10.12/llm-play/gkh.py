"""Golden Krone Hotel control CLI and dependency-free MCP stdio adapter."""
import argparse
import json
import os
from pathlib import Path
import sys
import urllib.error
import urllib.request
import uuid

DEFAULT_CONNECTION = Path(os.environ.get('LOCALAPPDATA', Path.home())) / 'GoldenKroneLLM' / 'connection.json'


class Bridge:
    def __init__(self, connection=DEFAULT_CONNECTION):
        self.connection = Path(connection)

    def call(self, endpoint, data=None):
        try:
            info = json.loads(self.connection.read_text(encoding='utf-8'))
        except (OSError, ValueError) as exc:
            raise RuntimeError('Game bridge is unavailable. Launch/restart the modded game first.') from exc
        # A connection file cannot redirect the token or actions to a remote host.
        from urllib.parse import urlsplit
        address = urlsplit(info['url'])
        if address.scheme != 'http' or address.hostname != '127.0.0.1' or not address.port or address.path or address.query or address.fragment or address.username:
            raise RuntimeError('Invalid local bridge address')
        request = urllib.request.Request(info['url'] + endpoint,
            data=None if data is None else json.dumps(data).encode('utf-8'),
            headers={'Authorization': 'Bearer ' + info['token'], 'Content-Type': 'application/json'})
        try:
            # Ignore HTTP_PROXY: communication is strictly local.
            with urllib.request.build_opener(urllib.request.ProxyHandler({})).open(request, timeout=20) as response:
                return json.load(response)
        except urllib.error.HTTPError as exc:
            try:
                message = json.load(exc).get('error', str(exc))
            except ValueError:
                message = str(exc)
            raise RuntimeError(message) from exc
        except (urllib.error.URLError, TimeoutError) as exc:
            raise RuntimeError('Bridge connection failed. Ensure the modded game is running; observe before retrying an action.') from exc


def schema(properties=None, required=None):
    return {'type': 'object', 'properties': properties or {}, 'required': required or [], 'additionalProperties': False}


TOOLS = [
    {'name': 'gkh_observe', 'description': 'Read game state, visible and remembered terrain, player stats, potions, abilities, messages, targeting and menu choices. Read before every action. x grows east; y grows south.',
     'inputSchema': schema()},
    {'name': 'gkh_act', 'description': 'One normal game input. Supply revision from observe and a unique requestId; retry the SAME payload/id after transport failure. type command uses a command from state.commands; choose selects a zero-based menu index; target aims at visible x,y (then command CONFIRM_TARGET to fire/cast/examine). Inspect returned state for success; commands may have no effect. REST can spend up to 50 turns. Never act when busy.',
     'inputSchema': schema({'type': {'type': 'string', 'enum': ['command', 'choose', 'target']},
        'command': {'type': 'string'}, 'index': {'type': 'integer', 'minimum': 0},
        'x': {'type': 'integer'}, 'y': {'type': 'integer'}, 'shift': {'type': 'boolean'},
        'expectedRevision': {'type': 'integer'}, 'requestId': {'type': 'string', 'pattern': '^[A-Za-z0-9_-]{1,80}$'}},
        ['type', 'expectedRevision', 'requestId'])},
    {'name': 'gkh_control', 'description': 'Acquire exclusive LLM control with enabled:true, exclusive:true before playing. Local keyboard/mouse/controller input is locked; user can watch. Pause with enabled:false (retains lock). Release with enabled:false, exclusive:false; accepted move finishes before local input unlocks. User Take control button also releases and pauses. Respect that handoff: do not automatically reacquire. Observe remains available. Ownership persists across restart, locked and paused.',
     'inputSchema': schema({'enabled': {'type': 'boolean'}, 'exclusive': {'type': 'boolean'}}, ['enabled'])},
    {'name': 'gkh_seed_observe', 'description': 'Seed-solving omniscience: full CURRENT floor, including unseen terrain/items/creatures and true carried potion identities. Includes normal state/revision and seedHarness timeline status. Requires active seed aids in a non-tutorial run. Does not reveal future floors or identify potions in gameplay.',
     'inputSchema': schema()},
    {'name': 'gkh_seed_forecast', 'description': 'Simulate nine possible next movement/adjacent attack/WAIT inputs from current RNG, then restore world and RNG. hp is signed player HP change; damage is outgoing damage. Results may be disabled, blocked or stop at a prompt. Does not forecast spells, potions, stairs or long plans. Supply latest observation revision. Requires idle play with no pending choice.',
     'inputSchema': schema({'expectedRevision': {'type': 'integer'}}, ['expectedRevision'])},
    {'name': 'gkh_seed_control', 'description': 'Seed aids enable, undo or redo. Each undo/redo travels ONE recorded position, or undo cancels an unfinished choice. Redo refuses a dead position; undo can recover ordinary-play death. Check state.seedHarness canUndo/canRedo/cursor/length/pending. Actual actions after undo discard redo branches. World/RNG restore; permanent earned progression and external effects are not rolled back. Supply observed revision and unique requestId; retry identical payload/id after transport failure.',
     'inputSchema': schema({'operation': {'type': 'string', 'enum': ['enable', 'undo', 'redo']},
         'expectedRevision': {'type': 'integer'}, 'requestId': {'type': 'string', 'pattern': '^[A-Za-z0-9_-]{1,80}$'}},
         ['operation', 'expectedRevision', 'requestId'])}
]


def tool_call(bridge, name, args):
    spec = next((t for t in TOOLS if t['name'] == name), None)
    if spec is None:
        raise ValueError('Unknown tool')
    if not isinstance(args, dict):
        raise ValueError('arguments must be an object')
    rules = spec['inputSchema']
    if set(args) - set(rules['properties']) or set(rules['required']) - set(args):
        raise ValueError('Missing required or unexpected arguments')
    if name == 'gkh_observe':
        return bridge.call('/state')
    if name == 'gkh_seed_observe':
        return bridge.call('/seed/state')
    if name == 'gkh_seed_forecast':
        revision = args['expectedRevision']
        if isinstance(revision, bool) or not isinstance(revision, int) or revision < 0:
            raise ValueError('expectedRevision must be a nonnegative integer')
        return bridge.call('/seed/forecast?revision=' + str(revision))
    if name == 'gkh_seed_control':
        return bridge.call('/seed/control', {'type': 'seed', **args})
    if name == 'gkh_seed_act':
        return bridge.call('/seed/action', args)
    if name == 'gkh_control':
        if not isinstance(args['enabled'], bool) or 'exclusive' in args and not isinstance(args['exclusive'], bool):
            raise ValueError('enabled and exclusive must be boolean')
        if 'exclusive' in args and not isinstance(bridge.call('/state').get('exclusive'), bool):
            raise RuntimeError('Exclusive control is unavailable in the running game. Restart the game to load the input lock.')
        return bridge.call('/control', args)
    return bridge.call('/action' if name == 'gkh_act' else '/control', args)


TOOLS.append({'name': 'gkh_seed_act',
    'description': 'Seed-solving gameplay input with mandatory ONE-MOVE death prevention. Same command/choose/target fields as gkh_act. Simulates the exact input and restores world/RNG before execution; refuses a fatal next move or an unavailable check. Menu/target setup consumes no turn; completing choices/casts is checked separately. REST and shift movement are refused: use WAIT or one unshifted move. Normal gkh_act has no death guard. Refusal consumes no turn/resources/history. Use observed revision and unique requestId.',
    'inputSchema': TOOLS[1]['inputSchema']})


def mode_tools(mode):
    return [t for t in TOOLS if mode == 'all' or t['name'] == 'gkh_control' or
            (t['name'].startswith('gkh_seed_') if mode == 'seed' else not t['name'].startswith('gkh_seed_'))]


def rpc(bridge, message, tool_mode='all'):
    if not isinstance(message, dict) or message.get('jsonrpc') != '2.0' or not isinstance(message.get('method'), str):
        return {'jsonrpc': '2.0', 'id': None, 'error': {'code': -32600, 'message': 'Invalid Request'}}
    if 'id' not in message:
        return None
    response = {'jsonrpc': '2.0', 'id': message['id']}
    method = message['method']
    params = message.get('params', {})
    try:
        if not isinstance(params, dict):
            raise ValueError('params must be an object')
        if method == 'initialize':
            requested = params.get('protocolVersion')
            supported = ['2024-11-05', '2025-03-26', '2025-06-18', '2025-11-25']
            response['result'] = {'protocolVersion': requested if requested in supported else '2025-03-26',
                'capabilities': {'tools': {}}, 'serverInfo': {'name': 'golden-krone-hotel', 'version': '1.3.0'},
                'instructions': 'Acquire exclusive ownership before authorized play: gkh_control enabled:true, exclusive:true, then observe a fresh revision. User Take control pauses and returns local input; respect that handoff and never automatically reacquire. Normal mode: gkh_observe/gkh_act, no hidden information or death guard. Seed mode: gkh_seed_observe/forecast/act/control, omniscience, one-move death prevention and undo/redo. Use seed_act for ALL seed gameplay inputs, never normal act as a fallback for refusal. Seed redo also refuses dead positions. Read before each input and inspect the outcome. Use normal tools to start a run before connecting seed-only mode.'}
        elif method == 'ping':
            response['result'] = {}
        elif method == 'tools/list':
            response['result'] = {'tools': mode_tools(tool_mode)}
        elif method == 'tools/call':
            try:
                if params.get('name') not in {t['name'] for t in mode_tools(tool_mode)}:
                    raise ValueError('Tool is unavailable in this mode')
                result = tool_call(bridge, params.get('name'), params.get('arguments', {}))
                response['result'] = {'content': [{'type': 'text', 'text': json.dumps(result, ensure_ascii=False)}]}
            except (RuntimeError, ValueError, KeyError, TypeError) as exc:
                response['result'] = {'isError': True, 'content': [{'type': 'text', 'text': str(exc)}]}
        else:
            response['error'] = {'code': -32601, 'message': 'Method not found'}
    except (ValueError, TypeError, KeyError) as exc:
        response['error'] = {'code': -32602, 'message': str(exc)}
    return response


def serve(bridge, tool_mode='all'):
    sys.stdin.reconfigure(encoding='utf-8')
    sys.stdout.reconfigure(encoding='utf-8')
    for line in sys.stdin:
        try:
            message = json.loads(line)
            if isinstance(message, list):
                response = [r for m in message if (r := rpc(bridge, m, tool_mode)) is not None]
                if not response:
                    continue
            else:
                response = rpc(bridge, message, tool_mode)
        except ValueError:
            response = {'jsonrpc': '2.0', 'id': None, 'error': {'code': -32700, 'message': 'Parse error'}}
        if response is not None:
            print(json.dumps(response, ensure_ascii=False), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--connection', type=Path, default=DEFAULT_CONNECTION)
    sub = parser.add_subparsers(dest='mode', required=True)
    sub.add_parser('state')
    sub.add_parser('seed-state')
    forecast = sub.add_parser('seed-forecast')
    forecast.add_argument('--revision', type=int, help='Revision used to decide; defaults to a fresh observation')
    mcp = sub.add_parser('mcp')
    mcp.add_argument('--tool-mode', choices=['normal', 'seed', 'all'], default='all', help='Expose only normal tools, only seed tools, or both families')
    control = sub.add_parser('control')
    control.add_argument('value', choices=['enable', 'pause', 'takeover', 'release'])
    action = sub.add_parser('action')
    action.add_argument('payload', help='JSON action; requestId and expectedRevision may be omitted for CLI use')
    seed_action = sub.add_parser('seed-action')
    seed_action.add_argument('payload', help='JSON seed operation: enable, undo, or redo; requestId/revision may be omitted')
    seed_play = sub.add_parser('seed-play')
    seed_play.add_argument('payload', help='JSON gameplay input with a mandatory one-move death check')
    args = parser.parse_args()
    bridge = Bridge(args.connection)
    if args.mode == 'mcp':
        serve(bridge, args.tool_mode)
        return
    try:
        if args.mode == 'state':
            result = bridge.call('/state')
        elif args.mode == 'seed-state':
            result = bridge.call('/seed/state')
        elif args.mode == 'seed-forecast':
            revision = args.revision if args.revision is not None else bridge.call('/state')['revision']
            result = tool_call(bridge, 'gkh_seed_forecast', {'expectedRevision': revision})
        elif args.mode == 'control':
            ownership = {'exclusive': args.value == 'takeover'} if args.value in ('takeover', 'release') else {}
            result = tool_call(bridge, 'gkh_control', {'enabled': args.value in ('enable', 'takeover'), **ownership})
        else:
            payload = json.loads(args.payload)
            if not isinstance(payload, dict):
                raise ValueError('Action must be a JSON object')
            if args.mode == 'seed-action':
                if payload.get('type', 'seed') != 'seed':
                    raise ValueError('seed-action requires type seed')
                payload['type'] = 'seed'
            payload.setdefault('requestId', uuid.uuid4().hex)
            if 'expectedRevision' not in payload:
                payload['expectedRevision'] = bridge.call('/state')['revision']
            # Record before sending so a lost reply can be retried with the same id.
            print('Action request: ' + json.dumps(payload), file=sys.stderr)
            endpoint = '/seed/control' if args.mode == 'seed-action' else '/seed/action' if args.mode == 'seed-play' else '/action'
            result = bridge.call(endpoint, payload)
        print(json.dumps(result, indent=2, ensure_ascii=False))
    except (RuntimeError, ValueError, KeyError, TypeError) as exc:
        print(str(exc), file=sys.stderr)
        sys.exit(1)


if __name__ == '__main__':
    main()
