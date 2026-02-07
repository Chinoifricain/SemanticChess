var WebSocketPlugin = {

    $wsInstances: {},
    $wsQueues: {},
    $wsNextId: [1],

    WebSocket_Connect: function (urlPtr) {
        var url = UTF8ToString(urlPtr);
        var id = wsNextId[0]++;
        wsQueues[id] = [];

        var ws = new WebSocket(url);

        ws.onopen = function () {
            wsQueues[id].push('{"type":"__open"}');
        };

        ws.onmessage = function (e) {
            wsQueues[id].push(e.data);
        };

        ws.onclose = function (e) {
            wsQueues[id].push('{"type":"__close","code":' + e.code + '}');
        };

        ws.onerror = function () {
            wsQueues[id].push('{"type":"__error"}');
        };

        wsInstances[id] = ws;
        return id;
    },

    WebSocket_Send: function (id, msgPtr) {
        var ws = wsInstances[id];
        if (ws && ws.readyState === 1) {
            ws.send(UTF8ToString(msgPtr));
        }
    },

    WebSocket_Close: function (id) {
        var ws = wsInstances[id];
        if (ws) {
            ws.close();
        }
    },

    WebSocket_GetState: function (id) {
        var ws = wsInstances[id];
        return ws ? ws.readyState : 3;
    },

    WebSocket_GetNextMessage: function (id) {
        var q = wsQueues[id];
        if (!q || q.length === 0) return 0;

        var msg = q.shift();
        var bufferSize = lengthBytesUTF8(msg) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(msg, buffer, bufferSize);
        return buffer;
    },

    WebSocket_FreeMsgBuffer: function (ptr) {
        _free(ptr);
    }
};

autoAddDeps(WebSocketPlugin, '$wsInstances');
autoAddDeps(WebSocketPlugin, '$wsQueues');
autoAddDeps(WebSocketPlugin, '$wsNextId');
mergeInto(LibraryManager.library, WebSocketPlugin);
