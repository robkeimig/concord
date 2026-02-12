const wsProtocol = location.protocol === "https:" ? "wss:" : "ws:";
const ws = new WebSocket(`${wsProtocol}//${location.host}/ws`);

const localVideo = document.getElementById("localVideo");
const remoteVideo = document.getElementById("remoteVideo");
const shareBtn = document.getElementById("shareBtn");
const stopShareBtn = document.getElementById("stopShareBtn");
const disconnectBtn = document.getElementById("disconnectBtn");
const streamList = document.getElementById("streamList");
const statusEl = document.getElementById("status");
const viewingLabel = document.getElementById("viewingLabel");

const rtcConfig = {
    iceServers: [{ urls: "stun:stun.l.google.com:19302" }]
};

// Sender tuning (screen-share)
const SCREEN_SENDER_MAX_BITRATE_BPS = 20_000_000; // ~2.5 Mbps starting point
const SCREEN_SENDER_MAX_FRAMERATE = 60;

let selfId = null;
let isStreaming = false;
let localStream = null;

// We support two roles:
// - viewer: connected to exactly one remote streamer at a time (single `viewingPeerId`)
// - streamer: can have multiple viewers; each viewer gets their own RTCPeerConnection
let viewingPeerId = null;
let viewerPc = null;
const streamerPcs = new Map(); // peerId(viewer) -> RTCPeerConnection

function setStatus(text) {
    statusEl.textContent = text;
}

function setViewing(peerId) {
    viewingPeerId = peerId;
    viewingLabel.textContent = peerId ? peerId : "(none)";
    disconnectBtn.disabled = !peerId;
}

function canSend() {
    return ws.readyState === WebSocket.OPEN;
}

function send(msg) {
    if (!canSend()) return;
    ws.send(JSON.stringify(msg));
}

ws.onopen = () => setStatus("connected");
ws.onclose = () => setStatus("disconnected");
ws.onerror = () => setStatus("error");

ws.onmessage = async (event) => {
    const msg = JSON.parse(event.data);

    switch (msg.type) {
        case "hello":
            selfId = msg.clientId;
            setStatus(`connected as ${selfId}`);
            renderStreams(msg.streamers || []);
            break;

        case "streamers":
            renderStreams(msg.streamers || []);
            break;

        case "peer-left":
            if (msg.clientId) {
                if (msg.clientId === viewingPeerId) {
                    await disconnectViewer();
                }
                await closeStreamerPc(msg.clientId);
            }
            break;

        case "offer":
            if (!msg.from) return;
            await onOffer(msg.from, msg.offer);
            break;

        case "answer":
            if (!msg.from) return;
            await onAnswer(msg.from, msg.answer);
            break;

        case "ice":
            if (!msg.from) return;
            await onIce(msg.from, msg.candidate);
            break;

        case "hangup":
            if (msg.from) {
                if (msg.from === viewingPeerId) {
                    await disconnectViewer();
                }
                await closeStreamerPc(msg.from);
            }
            break;

        default:
            break;
    }
};

function renderStreams(streamers) {
    const filtered = (streamers || []).filter(id => id && id !== selfId);

    streamList.innerHTML = "";

    if (filtered.length === 0) {
        const empty = document.createElement("div");
        empty.className = "streamRow muted";
        empty.textContent = "No one is streaming.";
        streamList.appendChild(empty);
        return;
    }

    for (const id of filtered) {
        const row = document.createElement("div");
        row.className = "streamRow";

        const label = document.createElement("div");
        label.className = "streamLabel";
        label.textContent = id;

        const btn = document.createElement("button");
        btn.textContent = (viewingPeerId === id) ? "Viewing" : "View";
        btn.disabled = (viewingPeerId === id);
        btn.onclick = async () => {
            await viewStream(id);
        };

        row.appendChild(label);
        row.appendChild(btn);
        streamList.appendChild(row);
    }
}

function wireCommonPcHandlers(pc, peerId, { isViewer }) {
    pc.onicecandidate = e => {
        if (e.candidate) send({ type: "ice", to: peerId, candidate: e.candidate });
    };

    pc.ontrack = e => {
        if (isViewer) {
            remoteVideo.srcObject = e.streams[0];
        }
    };
}

function applyScreenSenderTuning(sender) {
    if (!sender || !sender.track || sender.track.kind !== "video") return;

    const params = sender.getParameters();
    params.encodings ??= [{}];

    // Prefer maintaining framerate under constrained bandwidth.
    params.degradationPreference = "maintain-framerate-and-resolution";

    // Conservative defaults to reduce jitter on typical consumer links.
    params.encodings[0].maxBitrate = SCREEN_SENDER_MAX_BITRATE_BPS;
    params.encodings[0].maxFramerate = SCREEN_SENDER_MAX_FRAMERATE;

    // Some browsers reject unsupported parameter combinations; ignore failures.
    sender.setParameters(params).catch(() => { });
}

async function createViewerPc(peerId) {
    if (viewerPc) return viewerPc;

    viewerPc = new RTCPeerConnection(rtcConfig);
    wireCommonPcHandlers(viewerPc, peerId, { isViewer: true });

    return viewerPc;
}

async function createStreamerPc(viewerId) {
    let pc = streamerPcs.get(viewerId);
    if (pc) return pc;

    pc = new RTCPeerConnection(rtcConfig);
    wireCommonPcHandlers(pc, viewerId, { isViewer: false });

    if (localStream) {
        for (const track of localStream.getTracks()) {
            const sender = pc.addTrack(track, localStream);
            applyScreenSenderTuning(sender);
        }
    }

    streamerPcs.set(viewerId, pc);
    return pc;
}

async function onOffer(fromPeerId, offer) {
    // If we are streaming, this offer is a viewer asking to see our stream.
    if (isStreaming && localStream) {
        const pc = await createStreamerPc(fromPeerId);
        await pc.setRemoteDescription(offer);
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        send({ type: "answer", to: fromPeerId, answer });
        return;
    }

    // Otherwise, we might be the viewer receiving an offer from a streamer (not expected in this flow).
    // Keep minimal support.
    if (viewingPeerId !== fromPeerId) {
        // Ignore unexpected offers.
        return;
    }

    const pc = await createViewerPc(fromPeerId);
    await pc.setRemoteDescription(offer);
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    send({ type: "answer", to: fromPeerId, answer });
}

async function onAnswer(fromPeerId, answer) {
    // Viewer receives answer from the streamer it requested.
    if (viewingPeerId === fromPeerId && viewerPc) {
        await viewerPc.setRemoteDescription(answer);
        return;
    }

    // Streamer receives answer from a viewer (not used; streamer answers viewer offers).
}

async function onIce(fromPeerId, candidate) {
    if (!candidate) return;

    if (viewingPeerId === fromPeerId && viewerPc) {
        try { await viewerPc.addIceCandidate(candidate); } catch { }
        return;
    }

    const streamerPc = streamerPcs.get(fromPeerId);
    if (streamerPc) {
        try { await streamerPc.addIceCandidate(candidate); } catch { }
    }
}

async function viewStream(peerId) {
    if (viewingPeerId && viewingPeerId !== peerId) {
        await disconnectViewer();
    }

    setViewing(peerId);

    const pc = await createViewerPc(peerId);

    // Ensure we ask to receive video.
    const offer = await pc.createOffer({ offerToReceiveVideo: true });
    await pc.setLocalDescription(offer);
    send({ type: "offer", to: peerId, offer });

    renderStreams(getCurrentStreamerIdsFromUI());
}

function getCurrentStreamerIdsFromUI() {
    const ids = [];
    for (const el of streamList.querySelectorAll(".streamLabel")) {
        ids.push(el.textContent);
    }
    return ids;
}

async function disconnectViewer() {
    if (viewingPeerId) {
        send({ type: "hangup", to: viewingPeerId });
    }

    setViewing(null);

    if (viewerPc) {
        try { viewerPc.close(); } catch { }
        viewerPc = null;
    }

    remoteVideo.srcObject = null;
    renderStreams(getCurrentStreamerIdsFromUI());
}

async function closeStreamerPc(viewerId) {
    const pc = streamerPcs.get(viewerId);
    if (!pc) return;

    try { pc.close(); } catch { }
    streamerPcs.delete(viewerId);
}

shareBtn.onclick = async () => {
    if (isStreaming) return;

    localStream = await navigator.mediaDevices.getDisplayMedia({
        video: {
            frameRate: { ideal: SCREEN_SENDER_MAX_FRAMERATE, max: SCREEN_SENDER_MAX_FRAMERATE }
        },
        audio: false
    });

    localVideo.srcObject = localStream;

    isStreaming = true;
    shareBtn.disabled = true;
    stopShareBtn.disabled = false;

    send({ type: "stream-start" });

    // If we were watching someone else while starting to stream, that's fine.

    localStream.getVideoTracks()[0].onended = () => {
        stopSharing();
    };
};

stopShareBtn.onclick = async () => {
    await stopSharing();
};

async function stopSharing() {
    if (!isStreaming) return;

    isStreaming = false;
    shareBtn.disabled = false;
    stopShareBtn.disabled = true;

    // Close all streamer peer connections (viewers should reconnect when stream starts again)
    for (const [viewerId] of streamerPcs) {
        send({ type: "hangup", to: viewerId });
        await closeStreamerPc(viewerId);
    }

    if (localStream) {
        localStream.getTracks().forEach(t => t.stop());
        localStream = null;
    }

    localVideo.srcObject = null;

    send({ type: "stream-stop" });
}

disconnectBtn.onclick = async () => {
    await disconnectViewer();
};
