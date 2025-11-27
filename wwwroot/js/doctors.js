document.addEventListener('DOMContentLoaded', () => {
    if (!document.getElementById('doctorsList')) return;
    initDoctorsPage();
});

let bsModalInstance = null;

function initDoctorsPage() {
    loadDoctors();

    const modalEl = document.getElementById('doctorModal');
    bsModalInstance = new bootstrap.Modal(modalEl);
    const form = document.getElementById('doctorForm');
    const btnNew = document.getElementById('btnNewDoctor');

    btnNew.addEventListener('click', () => {
        openModalForNew();
        bsModalInstance.show();
    });

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        try {
            const success = await saveDoctor();
            if (success) {
                bsModalInstance.hide();
                await loadDoctors();
            }
            // If saveDoctor returns false (validation failed), keep modal open
        } catch (err) {
            // If saveDoctor throws (network/server error), keep modal open to show error
            console.error('Save error:', err);
        }
    });
}

async function loadDoctors() {
    const listEl = document.getElementById('doctorsList');
    listEl.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div><p class="mt-2 text-muted">Loading doctors...</p></div>';
    try {
        const r = await fetch('/api/doctors');
        if (!r.ok) {
            listEl.innerHTML = '<div class="text-center py-5"><i class="bi bi-exclamation-triangle text-danger" style="font-size: 3rem;"></i><p class="mt-3 text-danger">Failed to load doctors</p></div>';
            return;
        }
        const arr = await r.json();
        if (!arr.length) {
            listEl.innerHTML = '<div class="text-center py-5"><i class="bi bi-inbox text-muted" style="font-size: 3rem;"></i><p class="mt-3 text-muted">No doctors yet. Click "New Doctor" to add one.</p></div>';
            return;
        }
        listEl.innerHTML = arr.map(d => doctorItemHtml(d)).join('');
        document.querySelectorAll('.edit-doctor').forEach(btn => btn.addEventListener('click', onEdit));
        document.querySelectorAll('.delete-doctor').forEach(btn => btn.addEventListener('click', onDelete));
    } catch (err) {
        listEl.innerHTML = '<div class="text-center py-5"><i class="bi bi-exclamation-triangle text-danger" style="font-size: 3rem;"></i><p class="mt-3 text-danger">Error: ' + escapeHtml(err.message) + '</p></div>';
    }
}

function doctorItemHtml(d) {
    return `<div class="list-group-item d-flex justify-content-between align-items-center fade-in">
    <div class="d-flex align-items-center">
      <div class="me-3">
        <i class="bi bi-person-badge-fill text-primary" style="font-size: 2rem;"></i>
      </div>
      <div>
        <strong class="d-block fs-5">${escapeHtml(d.name)}</strong>
        <small class="text-muted"><i class="bi bi-briefcase me-1"></i>${escapeHtml(d.specialization)}</small>
      </div>
    </div>
    <div class="btn-group" role="group">
      <button class="btn btn-sm btn-outline-primary edit-doctor" data-id="${d.id}" title="Edit">
        <i class="bi bi-pencil me-1"></i>Edit
      </button>
      <button class="btn btn-sm btn-outline-danger delete-doctor" data-id="${d.id}" title="Delete">
        <i class="bi bi-trash me-1"></i>Delete
      </button>
    </div>
  </div>`;
}

function escapeHtml(s) { return s ? s.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;') : ''; }

function openModalForNew() {
    document.getElementById('modalTitle').innerHTML = '<i class="bi bi-person-plus me-2"></i>New Doctor';
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
    document.getElementById('modalTitle').innerHTML = '<i class="bi bi-pencil-square me-2"></i>Edit Doctor';
    document.getElementById('doctorId').value = d.id;
    document.getElementById('doctorName').value = d.name;
    document.getElementById('doctorSpec').value = d.specialization;
    document.getElementById('doctorError').innerText = '';

    // Use the existing modal instance or create one
    if (!bsModalInstance) {
        bsModalInstance = new bootstrap.Modal(document.getElementById('doctorModal'));
    }
    bsModalInstance.show();
}

async function saveDoctor() {
    const id = document.getElementById('doctorId').value;
    const name = document.getElementById('doctorName').value.trim();
    const specialization = document.getElementById('doctorSpec').value.trim();
    const errorEl = document.getElementById('doctorError');
    errorEl.innerText = '';

    // Validation
    if (!name || name.length < 2) {
        errorEl.innerText = 'Doctor name must be at least 2 characters.';
        return false; // Return false to indicate failure, don't close modal
    }
    if (name.length > 120) {
        errorEl.innerText = 'Doctor name cannot exceed 120 characters.';
        return false;
    }
    if (!specialization || specialization.length < 2) {
        errorEl.innerText = 'Specialization must be at least 2 characters.';
        return false;
    }
    if (specialization.length > 80) {
        errorEl.innerText = 'Specialization cannot exceed 80 characters.';
        return false;
    }

    const payload = {
        name: name,
        specialization: specialization
    };
    const url = id ? '/api/doctors/' + id : '/api/doctors';
    const method = id ? 'PUT' : 'POST';
    const r = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        credentials: 'same-origin',
        body: JSON.stringify(payload)
    });
    if (!r.ok) {
        const txt = await r.text();
        errorEl.innerText = txt || 'Save failed';
        return false; // Return false instead of throwing
    }

    return true; // Return true to indicate success, modal will close
}

async function onDelete(e) {
    if (!confirm('Delete doctor? This is permanent.')) return;
    const id = e.currentTarget.getAttribute('data-id');
    const r = await fetch('/api/doctors/' + id, {
        method: 'DELETE',
        credentials: 'same-origin'
    });
    if (r.status === 204) loadDoctors();
    else {
        const txt = await r.text();
        alert('Delete failed: ' + txt);
    }
}
