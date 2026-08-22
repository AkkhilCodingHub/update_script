// WinUpdate Web Dashboard Client Logic

let stepsData = [];
let activeCategory = 'all';
let autoScroll = true;
let isRunning = false;
let startTime = null;
let timerInterval = null;
let completedStepsCount = 0;

// Elements
const metaOs = document.getElementById('metaOs');
const metaHost = document.getElementById('metaHost');
const wsStatus = document.getElementById('wsStatus');
const wsStatusText = document.getElementById('wsStatusText');
const statAvailable = document.getElementById('statAvailable');
const statSuccess = document.getElementById('statSuccess');
const statDuration = document.getElementById('statDuration');
const statStatus = document.getElementById('statStatus');
const stepsList = document.getElementById('stepsList');
const terminal = document.getElementById('terminal');
const terminalOutput = document.getElementById('terminalOutput');
const currentStepLabel = document.getElementById('currentStepLabel');
const btnRunAll = document.getElementById('btnRunAll');
const btnDryRun = document.getElementById('btnDryRun');
const btnCancel = document.getElementById('btnCancel');
const btnClearLogs = document.getElementById('btnClearLogs');
const btnAutoScroll = document.getElementById('btnAutoScroll');

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    setupEventListeners();
    await fetchStatus();
    initSignalR();
});

function setupEventListeners() {
    btnRunAll.addEventListener('click', () => triggerRun(false));
    btnDryRun.addEventListener('click', () => triggerRun(true));
    btnCancel.addEventListener('click', cancelRun);
    btnClearLogs.addEventListener('click', () => {
        terminalOutput.innerHTML = '';
    });

    btnAutoScroll.addEventListener('click', () => {
        autoScroll = !autoScroll;
        btnAutoScroll.classList.toggle('active', autoScroll);
    });

    document.querySelectorAll('.filter-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            activeCategory = btn.dataset.category;
            renderSteps();
        });
    });
}

async function fetchStatus() {
    try {
        const res = await fetch('/api/status');
        if (!res.ok) return;
        const data = await res.json();

        metaOs.textContent = data.os || 'Windows';
        metaHost.textContent = data.machineName || 'Localhost';

        stepsData = (data.steps || []).map(s => ({
            ...s,
            status: 'pending',
            duration: 0,
            message: ''
        }));

        updateStats();
        renderSteps();
    } catch (err) {
        console.error('Failed to fetch status:', err);
    }
}

function initSignalR() {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/updateHub")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .build();

    connection.onreconnecting(() => {
        wsStatus.className = 'meta-pill connection-status';
        wsStatusText.textContent = 'Reconnecting...';
    });

    connection.onreconnected(() => {
        wsStatus.className = 'meta-pill connection-status connected';
        wsStatusText.textContent = 'Live Connected';
    });

    connection.onclose(() => {
        wsStatus.className = 'meta-pill connection-status';
        wsStatusText.textContent = 'Disconnected';
    });

    connection.on("StepStarting", data => {
        setRunningState(true);
        currentStepLabel.textContent = `Running: ${data.stepName}...`;
        appendLog(`\n▶ [${data.stepName}] Started (${data.category})`, 'step-start');

        const step = stepsData.find(s => s.id === data.stepId);
        if (step) {
            step.status = 'running';
            renderSteps();
        }
    });

    connection.on("StepLog", data => {
        appendLog(`  │ ${data.log}`);
    });

    connection.on("StepCompleted", data => {
        const step = stepsData.find(s => s.id === data.stepId);
        if (step) {
            step.status = data.status.toLowerCase();
            step.duration = data.duration;
            step.message = data.message;
            renderSteps();
        }

        if (data.status.toLowerCase() === 'success') {
            completedStepsCount++;
            statSuccess.textContent = completedStepsCount;
            appendLog(`✔ SUCCESS (${data.duration.toFixed(1)}s): ${data.message}`, 'success');
        } else if (data.status.toLowerCase() === 'warning') {
            appendLog(`⚠ WARNING (${data.duration.toFixed(1)}s): ${data.message}`, 'warning');
        } else if (data.status.toLowerCase() === 'failed') {
            appendLog(`✖ FAILED (${data.duration.toFixed(1)}s): ${data.message}`, 'error');
        } else {
            appendLog(`○ SKIPPED: ${data.message}`, 'system-msg');
        }
    });

    connection.on("RunCompleted", data => {
        setRunningState(false);
        currentStepLabel.textContent = 'Execution finished';
        statStatus.textContent = 'Finished';
        statStatus.className = 'stat-value text-success';
        appendLog(`\n======================================================`, 'system-msg');
        appendLog(`⚡ Run completed in ${data.totalDuration.toFixed(1)}s. Success: ${data.success}, Warnings: ${data.warning}, Failed: ${data.failed}, Skipped: ${data.skipped}`, 'success');
        appendLog(`======================================================\n`, 'system-msg');
    });

    connection.on("RunError", err => {
        setRunningState(false);
        appendLog(`❌ Fatal Run Error: ${err}`, 'error');
    });

    connection.start()
        .then(() => {
            wsStatus.className = 'meta-pill connection-status connected';
            wsStatusText.textContent = 'Live Connected';
        })
        .catch(err => {
            console.error('SignalR start failed:', err);
            wsStatusText.textContent = 'Offline';
        });
}

function renderSteps() {
    stepsList.innerHTML = '';

    const filtered = stepsData.filter(s => activeCategory === 'all' || s.category === activeCategory);

    filtered.forEach(step => {
        const card = document.createElement('div');
        card.className = `step-card ${step.status}`;

        let badgeContent = step.status;
        if (step.status === 'running') {
            badgeContent = `<span class="spinner"></span> Running`;
        }

        card.innerHTML = `
            <div class="step-info">
                <div class="step-title-row">
                    <span class="step-name">${step.name}</span>
                    <span class="category-tag">${step.category}</span>
                </div>
                <span class="step-desc">${step.description}</span>
            </div>
            <div class="step-badge badge-${step.status}">
                ${badgeContent}
            </div>
        `;

        stepsList.appendChild(card);
    });
}

function updateStats() {
    const available = stepsData.filter(s => s.isAvailable).length;
    statAvailable.textContent = `${available} / ${stepsData.length}`;
}

function appendLog(text, className = '') {
    const entry = document.createElement('div');
    entry.className = `log-entry ${className}`;
    entry.textContent = text;
    terminalOutput.appendChild(entry);

    if (autoScroll) {
        terminal.scrollTop = terminal.scrollHeight;
    }
}

async function triggerRun(dryRun = false) {
    if (isRunning) return;

    completedStepsCount = 0;
    statSuccess.textContent = '0';
    stepsData.forEach(s => s.status = 'pending');
    renderSteps();

    setRunningState(true);
    statStatus.textContent = dryRun ? 'Dry-Run Check' : 'Updating...';
    statStatus.className = 'stat-value text-info';
    appendLog(`\n[System] Initiating update orchestration (DryRun=${dryRun})...`, 'system-msg');

    try {
        const res = await fetch('/api/run', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ dryRun: dryRun })
        });
        if (!res.ok) {
            const err = await res.json();
            appendLog(`❌ Failed to start: ${err.error || 'Unknown error'}`, 'error');
            setRunningState(false);
        }
    } catch (err) {
        appendLog(`❌ Connection error: ${err}`, 'error');
        setRunningState(false);
    }
}

async function cancelRun() {
    try {
        await fetch('/api/cancel', { method: 'POST' });
        appendLog(`[System] Cancellation requested...`, 'warning');
    } catch (err) {
        console.error(err);
    }
}

function setRunningState(running) {
    isRunning = running;
    btnRunAll.disabled = running;
    btnDryRun.disabled = running;
    btnCancel.style.display = running ? 'inline-flex' : 'none';

    if (running) {
        startTime = Date.now();
        timerInterval = setInterval(() => {
            const elapsed = ((Date.now() - startTime) / 1000).toFixed(1);
            statDuration.textContent = `${elapsed}s`;
        }, 100);
    } else {
        if (timerInterval) {
            clearInterval(timerInterval);
            timerInterval = null;
        }
    }
}
