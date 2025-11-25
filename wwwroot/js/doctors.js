document.addEventListener('DOMContentLoaded', () => {
    if (!document.getElementById('doctorsList')) return;
    initDoctorsPage();
});

function initDoctorsPage() {
    loadDoctors();

    const modalEl = document.getElementById('doctorModal');
    const bsModal = new bootstrap.Modal(modalEl);
    const form = document.getElementById('doctorForm');
    const btnNew = document.getElementById('btnNewDoctor');

    btnNew.addEventListener('click', () => {
        openModalForNew();
        bsModal.show();
    });

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        await saveDoctor();
        bsModal.hide();
        await loadDoctors();
    });
}

async function loadDoctors() {
    const listEl = document.getElementById('doctorsList');
    listEl.innerHTML = 'Loading...';
    try {
        const r = await fetch('/api/doctors');
        if (!r.ok) { listEl.innerText = 'Failed to load doctors'; return; }
        const arr = await r.json();
        if (!arr.length) { listEl.innerText = 'No doctors yet'; return; }
        listEl.innerHTML = arr.map(d => doctorItemHtml(d)).join('');
        document.querySelectorAll('.edit-doctor').forEach(btn => btn.addEventListener('click', onEdit));
        document.querySelectorAll('.delete-doctor').forEach(btn => btn.addEventListener('click', onDelete));
    } catch (err) {
        listEl.innerText = 'Error: ' + err.message;
    }
}

function doctorItemHtml(d) {
    return `<div class="list-group-item d-flex justify-content-between align-items-center">
    <div>
      <strong>${escapeHtml(d.name)}</strong><div class="text-muted">${escapeHtml(d.specialization)}</div>
    </div>
    <div>
      <button class="btn btn-sm btn-outline-secondary edit-doctor" data-id="${d.id}">Edit</button>
      <button class="btn btn-sm btn-outline-danger delete-doctor" data-id="${d.id}">Delete</button>
    </div>
  </div>`;
}

function escapeHtml(s) { return s ? s.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;') : ''; }

function openModalForNew() {
    document.getElementById('modalTitle').innerText = 'New Doctor';
    document.getElementById('doctorId').value = '';
    document.getElementById('doctorName').value = '';
    document.getElementById('doctorSpec').value = '';
    document.getElementById('doctorError').innerText = '';
}

async function onEdit(e) {
    const id = e.currentTarget.getAttribute('data-id');
    const r = await fetch('/api/doctors/' + id);
    if (!r.ok) return alert('Could not load doctor');
    const d = await r.json();
    document.getElementById('modalTitle').innerText = 'Edit Doctor';
    document.getElementById('doctorId').value = d.id;
    document.getElementById('doctorName').value = d.name;
    document.getElementById('doctorSpec').value = d.specialization;
    const bsModal = new bootstrap.Modal(document.getElementById('doctorModal'));
    bsModal.show();
}

async function saveDoctor() {
    const id = document.getElementById('doctorId').value;
    const payload = {
        name: document.getElementById('doctorName').value.trim(),
        specialization: document.getElementById('doctorSpec').value.trim()
    };
    const url = id ? '/api/doctors/' + id : '/api/doctors';
    const method = id ? 'PUT' : 'POST';
    const r = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!r.ok) {
        const txt = await r.text();
        document.getElementById('doctorError').innerText = txt || 'Save failed';
        throw new Error('Save failed');
    }
}

async function onDelete(e) {
    if (!confirm('Delete doctor? This is permanent.')) return;
    const id = e.currentTarget.getAttribute('data-id');
    const r = await fetch('/api/doctors/' + id, { method: 'DELETE' });
    if (r.status === 204) loadDoctors();
    else {
        const txt = await r.text();
        alert('Delete failed: ' + txt);
    }
}
