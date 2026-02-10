const wsProtocol = location.protocol === "https:" ? "wss:" : "ws:";
const ws = new WebSocket(`${wsProtocol}//${location.host}/ws`);

const localVideo = document.getElementById("localVideo");
const remoteVideo = document.getElementById("remoteVideo");
const shareBtn = document.getElementById("shareBtn");

const rtcConfig = {
    iceServers: [{ urls: "stun:stun.l.google.com:19302" }]
};

let pc;
let makingOffer = false;

ensurePeerConnection();

ws.onmessage = async (event) => {
    const msg = JSON.parse(event.data);

    if (msg.type === "offer") {
        await pc.setRemoteDescription(msg.offer);
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);

        ws.send(JSON.stringify({
            type: "answer",
            answer
        }));
    }

    if (msg.type === "answer") {
        await pc.setRemoteDescription(msg.answer);
    }

    if (msg.type === "ice") {
        if (msg.candidate) {
            await pc.addIceCandidate(msg.candidate);
        }
    }
};

function ensurePeerConnection() {
    if (pc) return;

    pc = new RTCPeerConnection(rtcConfig);

    pc.onicecandidate = e => {
        if (e.candidate) {
            ws.send(JSON.stringify({
                type: "ice",
                candidate: e.candidate
            }));
        }
    };

    pc.ontrack = e => {
        remoteVideo.srcObject = e.streams[0];
    };

    pc.onnegotiationneeded = async () => {
        try {
            makingOffer = true;
            const offer = await pc.createOffer();
            await pc.setLocalDescription(offer);

            ws.send(JSON.stringify({
                type: "offer",
                offer
            }));
        } finally {
            makingOffer = false;
        }
    };
}

shareBtn.onclick = async () => {
    const stream = await navigator.mediaDevices.getDisplayMedia({
        video: {
            frameRate: { ideal: 60, max: 60 }
        },
        audio: false
    });

    for (const track of stream.getTracks()) {
        pc.addTrack(track, stream);
    }

    localVideo.srcObject = stream;

    stream.getVideoTracks()[0].onended = () => {
        stream.getTracks().forEach(t => t.stop());
    };
};
