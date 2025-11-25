document.addEventListener('DOMContentLoaded', () => {
    const path = location.pathname.toLowerCase();
    if (path === '/' || path.startsWith('/home')) {
    } else if (path.startsWith('/booking')) {
        initBookingPage();
    } else if (path.startsWith('/patient/search')) {
        initPatientSearch();
    } else if (path.startsWith('/admin/appointments')) {
        loadAdminAppointments();
    }
    loadDoctors();

    const doctorSelect = document.getElementById("doctorSelect");
    const dateInput = document.getElementById("date");

    doctorSelect.addEventListener("change", showAvailableSlots);
    dateInput.addEventListener("change", showAvailableSlots);

    document.getElementById("bookingForm").addEventListener("submit", bookAppointment);
});



async function loadAdminAppointments() {
    const r = await fetch('/api/admin/appointments', { credentials: 'same-origin' });
    if (r.status === 401 || r.status === 302) {
        // not logged in -> redirect to login (preserve returnUrl)
        const current = encodeURIComponent(location.pathname + location.search);
        location.href = '/Account/Login?returnUrl=' + current;
        return;
    }
    if (!r.ok) {
        document.getElementById('adminAppointmentsList').innerText = 'Could not load appointments';
        return;
    }
    const list = await r.json();
    const container = document.getElementById('adminAppointmentsList');
    if (!list.length) { container.innerText = 'No appointments'; return; }
    container.innerHTML = list.map(a => `<div class="d-flex justify-content-between align-items-center border p-2 mb-1">
    <div><strong>${new Date(a.startTime).toLocaleString()}</strong> — ${escapeHtml(a.doctor)} — ${escapeHtml(a.patient)} (FileNo:${escapeHtml(a.fileNo)})</div>
    <div><button class="btn btn-sm btn-danger" data-id="${a.id}">Cancel</button></div>
  </div>`).join('');
    container.querySelectorAll('button[data-id]').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const id = e.currentTarget.getAttribute('data-id');
            if (!confirm('Cancel appointment #' + id + '?')) return;
            const d = await fetch('/api/appointments/' + id, { method: 'DELETE', credentials: 'same-origin' });
            if (d.status === 204) loadAdminAppointments();
            else alert('Cancel failed');
        });
    });
}


async function initBookingPage() {
    await loadDoctors('#doctorSelect');
    document.getElementById('date').addEventListener('change', showAvailableSlots);
    document.getElementById('doctorSelect').addEventListener('change', showAvailableSlots);
    document.getElementById('bookingForm').addEventListener('submit', async (e) => {
        e.preventDefault();
        const doctorId = Number(document.getElementById('doctorSelect').value);
        const date = document.getElementById('date').value;
        const time = document.getElementById('time').value;
        if (!date || !time) { showResult('Pick date and time'); return; }
        const startIso = new Date(date + 'T' + time).toISOString();
        const payload = {
            PatientName: document.getElementById('patientName').value.trim(),
            Phone: document.getElementById('phone').value.trim(),
            DoctorId: doctorId,
            StartTime: startIso,
            DurationInMinutes: Number(document.getElementById('duration').value)
        };
        try {
            const res = await fetch('/api/appointments', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
            if (res.ok) {
                const data = await res.json();
                showResult('Booked successfully (id: ' + (data?.appointmentId ?? '') + ')', true);
                showAvailableSlots();
            } else {
                const txt = await res.text();
                showResult('Error: ' + txt);
            }
        } catch (err) { showResult('Network error: ' + err.message); }
    });
}

async function loadDoctors(selectSelector) {
    const r = await fetch('/api/doctors');
    const docs = await r.json();
    const sel = document.querySelector(selectSelector);
    if (!sel) return;
    sel.innerHTML = docs.map(d => `<option value="${d.id}">${escapeHtml(d.name)} (${escapeHtml(d.specialization)})</option>`).join('');
}



async function loadDoctors() {
    const sel = document.getElementById("doctorSelect");
    sel.innerHTML = `<option>Loading...</option>`;

    const r = await fetch("/api/doctors");
    if (!r.ok) { sel.innerHTML = `<option>Error loading</option>`; return; }

    const docs = await r.json();
    sel.innerHTML = `<option value="">Choose doctor...</option>` +
        docs.map(d => `<option value="${d.id}">${d.name} (${d.specialization})</option>`).join('');
}

async function showAvailableSlots() {
    const doctorId = Number(document.getElementById("doctorSelect").value);
    const date = document.getElementById("date").value;
    const container = document.getElementById("availableSlots");

    container.innerHTML = "";
    if (!doctorId || !date) return;

    container.innerHTML = "Loading...";

    const resp = await fetch(`/api/doctors/${doctorId}/availabletimes?date=${date}`);
    if (!resp.ok) {
        container.innerHTML = "Could not load slots";
        return;
    }

    const data = await resp.json();
    const slots = data.availableSlots || data.AvailableSlots || [];

    if (!slots.length) {
        container.innerHTML = "No available slots";
        return;
    }

    container.innerHTML = slots
        .map(s => `<button type="button" class="btn btn-outline-primary btn-sm m-1 slot-btn" data-iso="${s.iso}">${s.time}</button>`)
        .join("");

    document.querySelectorAll(".slot-btn").forEach(btn => {
        btn.addEventListener("click", () => {
            document.querySelectorAll(".slot-btn").forEach(x => x.classList.remove("active"));
            btn.classList.add("active");

            const iso = btn.getAttribute("data-iso");
            document.getElementById("selectedSlotIso").value = iso;

            // auto-fill the time input field
            const dt = new Date(iso);
            const t = dt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
            document.getElementById("time").value = t;
        });
    });
}

async function bookAppointment(e) {
    e.preventDefault();

    const doctorId = Number(document.getElementById("doctorSelect").value);
    const iso = document.getElementById("selectedSlotIso").value;
    const dateVal = document.getElementById("date").value;
    const timeVal = document.getElementById("time").value;
    const duration = Number(document.getElementById("duration").value);
    const patientName = document.getElementById("patientName").value.trim();
    const phone = document.getElementById("phone").value.trim();

    const resultEl = document.getElementById("bookingResult");
    resultEl.className = '';
    resultEl.textContent = '';

    // client-side checks (as before)
    if (!doctorId) return showError("Please select a doctor.");
    if (!iso && (!dateVal || !timeVal)) return showError("Please select or pick a date and time.");
    if (!patientName) return showError("Please enter patient name.");
    if (!/^\d+$/.test(phone)) return showError("Phone number must contain digits only (0-9).");

    // resolve start ISO (prefer selected slot)
    let startIso = iso;
    if (!startIso) {
        const combined = new Date(`${dateVal}T${timeVal}`);
        if (isNaN(combined.getTime())) return showError("Invalid date/time.");
        startIso = combined.toISOString();
    }

    // final future check
    if (new Date(startIso) <= new Date()) return showError("Selected time must be in the future.");

    const payload = {
        doctorId,
        startTime: startIso,
        durationInMinutes: duration,
        patient: { name: patientName, phone: phone }
    };

    try {
        const resp = await fetch("/api/appointments", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (resp.ok) {
            resultEl.className = 'text-success';
            resultEl.textContent = 'Appointment booked successfully!';
            // reload after 1 second
            setTimeout(() => {
                window.location.reload();
            }, 1000);
            // clear selection and refresh slots
            document.getElementById("selectedSlotIso").value = '';
            document.querySelectorAll('.slot-btn.active').forEach(x => x.classList.remove('active'));
            await showAvailableSlots();
            return;
        }

        // ---------- error handling ----------
        // attempt to parse JSON body first
        const ct = resp.headers.get("content-type") || "";
        let bodyText = await resp.text();
        let parsed = null;
        try { if (ct.includes("application/json") || ct.includes("application/problem+json")) parsed = JSON.parse(bodyText); }
        catch (err) { parsed = null; }

        // extract messages
        // 1) ASP.NET ProblemDetails / RFC7807 often has 'title' and 'detail'
        if (parsed && (parsed.title || parsed.detail)) {
            const title = parsed.title || parsed.detail || "Error";
            const detail = parsed.detail ? `<div>${escapeHtml(parsed.detail)}</div>` : "";
            showHtmlError(`<strong>${escapeHtml(title)}</strong>${detail}`);
            return;
        }

        // 2) ModelState errors: { errors: { "Patient": ["..."], "Phone":["..."] } }
        if (parsed && parsed.errors && typeof parsed.errors === "object") {
            const parts = [];
            for (const key of Object.keys(parsed.errors)) {
                const arr = parsed.errors[key];
                if (Array.isArray(arr)) {
                    for (const msg of arr) parts.push(`${escapeHtml(key)}: ${escapeHtml(msg)}`);
                }
            }
            if (parts.length) { showHtmlError(parts.map(p => `<div>${p}</div>`).join("")); return; }
        }

        // 3) custom { message: "..." } or { Message: "..." }
        if (parsed && (parsed.message || parsed.Message)) {
            showError(parsed.message || parsed.Message);
            return;
        }

        // 4) fallback: raw text or JSON stringified
        if (bodyText) {
            // if bodyText looks like JSON string of errors (your screenshot), JSON.parse may have produced object earlier
            showHtmlError(`<pre style="white-space:pre-wrap;margin:0">${escapeHtml(bodyText)}</pre>`);
            return;
        }

        // 5) generic
        showError(`Booking failed (HTTP ${resp.status}).`);
    } catch (err) {
        console.error(err);
        showError("Network error while booking. Please try again.");
    }

    // helper to set plain text error
    function showError(msg) {
        resultEl.className = 'text-danger';
        resultEl.textContent = msg;
    }

    // helper to set html (for multiple lines)
    function showHtmlError(html) {
        resultEl.className = 'text-danger';
        resultEl.innerHTML = html;
    }

    // simple escape helper
    function escapeHtml(s) {
        if (!s) return "";
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }
}

function showResult(msg, ok = false) {
    const el = document.getElementById('bookingResult');
    if (!el) return;
    el.textContent = msg;
    el.style.color = ok ? 'green' : 'red';
}

async function initPatientSearch() {
    document.getElementById('searchBtn').addEventListener('click', async () => {
        const fileNo = document.getElementById('fileNo').value.trim();
        const container = document.getElementById('patientResult');
        container.innerHTML = '';
        if (!fileNo) { container.innerText = 'Enter FileNo'; return; }
        const r = await fetch('/api/patients/' + encodeURIComponent(fileNo));
        if (r.status === 404) { container.innerText = 'Not found'; return; }
        const p = await r.json();
        container.innerHTML = `<div class="card p-2"><div><strong>${escapeHtml(p.name)}</strong> (FileNo: ${escapeHtml(p.fileNo)})</div><div>Phone: ${escapeHtml(p.phone)}</div></div>`;
    });
}

async function loadAdminAppointments() {
    const r = await fetch('/api/appointments');
    const list = await r.json();
    const container = document.getElementById('adminAppointmentsList');
    if (!list.items?.length) { container.innerText = 'No appointments'; return; }
    container.innerHTML = list.items.map(a => `<div class="d-flex justify-content-between align-items-center border p-2 mb-1"><div><strong>${new Date(a.startTime).toLocaleString()}</strong> — ${escapeHtml(a.doctor)} — ${escapeHtml(a.patient)} (FileNo:${escapeHtml(a.fileNo)})</div><div><button class="btn btn-sm btn-danger" data-id="${a.id}">Cancel</button></div></div>`).join('');
    container.querySelectorAll('button[data-id]').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const id = e.currentTarget.getAttribute('data-id');
            if (!confirm('Cancel appointment #' + id + '?')) return;
            const d = await fetch('/api/appointments/' + id, { method: 'DELETE' });
            if (d.status === 204) loadAdminAppointments();
            else alert('Cancel failed');
        });
    });
}

function escapeHtml(s) { return s ? s.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;') : ''; }
