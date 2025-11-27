document.addEventListener('DOMContentLoaded', () => {
    if (!document.getElementById('appointmentsList')) return;
    initAdminAppointments();
});

let currentPage = 1;
let currentFilters = {};
let appointmentModalInstance = null;

async function initAdminAppointments() {
    await loadDoctors();
    await loadAppointments();

    const modalEl = document.getElementById('appointmentModal');
    appointmentModalInstance = new bootstrap.Modal(modalEl);
    const form = document.getElementById('appointmentForm');
    const btnNew = document.getElementById('btnNewAppointment');
    const btnFilter = document.getElementById('btnFilter');

    btnNew.addEventListener('click', () => {
        openModalForNew();
        appointmentModalInstance.show();
    });

    btnFilter.addEventListener('click', () => {
        currentPage = 1;
        loadAppointments();
    });

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        try {
            const success = await saveAppointment();
            if (success) {
                appointmentModalInstance.hide();
                await loadAppointments();
            }
        } catch (err) {
            console.error('Save failed:', err);
        }
    });
}

async function loadDoctors() {
    try {
        const r = await fetch('/api/doctors');
        if (!r.ok) return;
        const doctors = await r.json();
        const select = document.getElementById('appointmentDoctor');
        const filterSelect = document.getElementById('filterDoctor');
        
        const options = '<option value="">Select Doctor...</option>' + 
            doctors.map(d => `<option value="${d.id}">${escapeHtml(d.name)} (${escapeHtml(d.specialization)})</option>`).join('');
        
        select.innerHTML = options;
        filterSelect.innerHTML = '<option value="">All Doctors</option>' + 
            doctors.map(d => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join('');
    } catch (err) {
        console.error('Failed to load doctors:', err);
    }
}

async function loadAppointments() {
    const listEl = document.getElementById('appointmentsList');
    listEl.innerHTML = '<div class="card-body p-0"><div class="text-center py-5"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div><p class="mt-2 text-muted">Loading appointments...</p></div></div>';

    try {
        const doctorId = document.getElementById('filterDoctor').value;
        const patientName = document.getElementById('filterPatient').value.trim();
        const dateFrom = document.getElementById('filterDateFrom').value;
        const dateTo = document.getElementById('filterDateTo').value;

        const params = new URLSearchParams({
            page: currentPage.toString(),
            pageSize: '20'
        });

        if (doctorId) params.append('doctorId', doctorId);
        if (patientName) params.append('patientName', patientName);
        if (dateFrom) params.append('dateFrom', dateFrom);
        if (dateTo) params.append('dateTo', dateTo);

        const r = await fetch('/api/appointments?' + params.toString(), {
            credentials: 'same-origin'
        });

        if (r.status === 401 || r.status === 403) {
            window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
            return;
        }

        if (!r.ok) {
            listEl.innerHTML = '<div class="card-body text-center py-5"><i class="bi bi-exclamation-triangle text-danger" style="font-size: 3rem;"></i><p class="mt-3 text-danger">Failed to load appointments</p></div>';
            return;
        }

        const data = await r.json();
        const appointments = data.items || [];

        if (appointments.length === 0) {
            listEl.innerHTML = '<div class="card-body text-center py-5"><i class="bi bi-inbox text-muted" style="font-size: 3rem;"></i><p class="mt-3 text-muted">No appointments found</p></div>';
            renderPagination(data);
            return;
        }

        listEl.innerHTML = `
            <div class="card-body p-0">
                <div class="table-responsive">
                    <table class="table table-hover align-middle mb-0">
                        <thead>
                            <tr>
                                <th><i class="bi bi-calendar-event me-1"></i>Date & Time</th>
                                <th><i class="bi bi-person-badge me-1"></i>Doctor</th>
                                <th><i class="bi bi-person me-1"></i>Patient</th>
                                <th><i class="bi bi-file-earmark me-1"></i>File No</th>
                                <th><i class="bi bi-clock me-1"></i>Duration</th>
                                <th><i class="bi bi-gear me-1"></i>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${appointments.map(a => appointmentRowHtml(a)).join('')}
                        </tbody>
                    </table>
                </div>
            </div>
        `;

        document.querySelectorAll('.edit-appointment').forEach(btn => {
            btn.addEventListener('click', (e) => onEdit(e, data.items));
        });
        document.querySelectorAll('.delete-appointment').forEach(btn => {
            btn.addEventListener('click', onDelete);
        });

        renderPagination(data);
    } catch (err) {
        listEl.innerHTML = '<div class="card-body text-center py-5"><i class="bi bi-exclamation-triangle text-danger" style="font-size: 3rem;"></i><p class="mt-3 text-danger">Error: ' + escapeHtml(err.message) + '</p></div>';
    }
}

function appointmentRowHtml(a) {
    const date = new Date(a.startTime);
    return `
        <tr class="fade-in">
            <td><strong>${date.toLocaleDateString()}</strong><br><small class="text-muted">${date.toLocaleTimeString()}</small></td>
            <td><i class="bi bi-person-badge text-primary me-1"></i>${escapeHtml(a.doctor)}</td>
            <td><i class="bi bi-person text-success me-1"></i>${escapeHtml(a.patient)}</td>
            <td><span class="badge bg-secondary">${escapeHtml(a.fileNo)}</span></td>
            <td><i class="bi bi-clock text-info me-1"></i>${a.durationInMinutes} min</td>
            <td>
                <div class="btn-group" role="group">
                    <button class="btn btn-sm btn-outline-primary edit-appointment" data-id="${a.id}" title="Edit">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger delete-appointment" data-id="${a.id}" title="Delete">
                        <i class="bi bi-trash"></i>
                    </button>
                </div>
            </td>
        </tr>
    `;
}

function renderPagination(data) {
    const nav = document.getElementById('paginationNav');
    if (!data.totalPages || data.totalPages <= 1) {
        nav.innerHTML = '';
        return;
    }

    let html = '<ul class="pagination justify-content-center">';
    
    // Previous
    html += `<li class="page-item ${currentPage === 1 ? 'disabled' : ''}">
        <a class="page-link" href="#" data-page="${currentPage - 1}">Previous</a>
    </li>`;

    // Page numbers
    for (let i = 1; i <= data.totalPages; i++) {
        if (i === 1 || i === data.totalPages || (i >= currentPage - 2 && i <= currentPage + 2)) {
            html += `<li class="page-item ${i === currentPage ? 'active' : ''}">
                <a class="page-link" href="#" data-page="${i}">${i}</a>
            </li>`;
        } else if (i === currentPage - 3 || i === currentPage + 3) {
            html += '<li class="page-item disabled"><span class="page-link">...</span></li>';
        }
    }

    // Next
    html += `<li class="page-item ${currentPage === data.totalPages ? 'disabled' : ''}">
        <a class="page-link" href="#" data-page="${currentPage + 1}">Next</a>
    </li>`;

    html += '</ul>';
    nav.innerHTML = html;

    nav.querySelectorAll('.page-link[data-page]').forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            const page = parseInt(e.target.getAttribute('data-page'));
            if (page > 0 && page <= data.totalPages) {
                currentPage = page;
                loadAppointments();
            }
        });
    });
}

function openModalForNew() {
    document.getElementById('modalTitle').innerHTML = '<i class="bi bi-calendar-plus me-2"></i>New Appointment';
    document.getElementById('appointmentId').value = '';
    document.getElementById('appointmentDoctor').value = '';
    document.getElementById('appointmentDate').value = '';
    document.getElementById('appointmentTime').value = '';
    document.getElementById('appointmentDuration').value = '30';
    document.getElementById('appointmentPatientFileNo').value = '';
    document.getElementById('appointmentPatientName').value = '';
    document.getElementById('appointmentPatientPhone').value = '';
    document.getElementById('appointmentPatientGender').value = '';
    document.getElementById('appointmentPatientAddress').value = '';
    document.getElementById('appointmentPatientDob').value = '';
    document.getElementById('appointmentError').innerText = '';
}

async function onEdit(e, appointments) {
    const id = parseInt(e.currentTarget.getAttribute('data-id'));
    const appointment = appointments.find(a => a.id === id);
    if (!appointment) {
        // Fetch full details
        const r = await fetch('/api/appointments/' + id, { credentials: 'same-origin' });
        if (!r.ok) {
            alert('Could not load appointment');
            return;
        }
        const fullAppt = await r.json();
        populateModal(fullAppt);
    } else {
        // Fetch full details for patient info
        const r = await fetch('/api/appointments/' + id, { credentials: 'same-origin' });
        if (r.ok) {
            const fullAppt = await r.json();
            populateModal(fullAppt);
        }
    }
    if (!appointmentModalInstance) {
        appointmentModalInstance = new bootstrap.Modal(document.getElementById('appointmentModal'));
    }
    appointmentModalInstance.show();
}

function populateModal(appt) {
    document.getElementById('modalTitle').innerHTML = '<i class="bi bi-pencil-square me-2"></i>Edit Appointment';
    document.getElementById('appointmentId').value = appt.id;
    document.getElementById('appointmentDoctor').value = appt.doctorId;
    
    const startTime = new Date(appt.startTime);
    document.getElementById('appointmentDate').value = startTime.toISOString().split('T')[0];
    document.getElementById('appointmentTime').value = startTime.toTimeString().slice(0, 5);
    document.getElementById('appointmentDuration').value = appt.durationInMinutes;
    
    document.getElementById('appointmentPatientFileNo').value = appt.patientFileNo || '';
    document.getElementById('appointmentPatientName').value = appt.patientName || '';
    document.getElementById('appointmentPatientPhone').value = appt.patientPhone || '';
    document.getElementById('appointmentPatientGender').value = appt.patientGender || '';
    document.getElementById('appointmentPatientAddress').value = appt.patientAddress || '';
    if (appt.patientDateOfBirth) {
        const dob = new Date(appt.patientDateOfBirth);
        document.getElementById('appointmentPatientDob').value = dob.toISOString().split('T')[0];
    }
    document.getElementById('appointmentError').innerText = '';
}

async function saveAppointment() {
    const id = document.getElementById('appointmentId').value;
    const errorEl = document.getElementById('appointmentError');
    errorEl.innerText = '';

    // Validation
    const doctorId = document.getElementById('appointmentDoctor').value;
    const date = document.getElementById('appointmentDate').value;
    const time = document.getElementById('appointmentTime').value;
    const duration = parseInt(document.getElementById('appointmentDuration').value);
    const patientName = document.getElementById('appointmentPatientName').value.trim();
    const fileNo = document.getElementById('appointmentPatientFileNo').value.trim();
    const phone = document.getElementById('appointmentPatientPhone').value.trim();
    const gender = document.getElementById('appointmentPatientGender').value;
    const address = document.getElementById('appointmentPatientAddress').value.trim();
    const dob = document.getElementById('appointmentPatientDob').value;

    if (!doctorId) { errorEl.innerText = 'Please select a doctor.'; return false; }
    if (!date || !time) { errorEl.innerText = 'Please select date and time.'; return false; }
    if (!duration || duration < 5) { errorEl.innerText = 'Duration must be at least 5 minutes.'; return false; }
    if (!patientName) { errorEl.innerText = 'Patient name is required.'; return false; }
    if (!fileNo) { errorEl.innerText = 'File number is required.'; return false; }
    if (!phone || !/^\d+$/.test(phone)) { errorEl.innerText = 'Valid phone number is required (digits only).'; return false; }
    if (!gender) { errorEl.innerText = 'Gender is required.'; return false; }
    if (!address) { errorEl.innerText = 'Address is required.'; return false; }
    if (!dob) { errorEl.innerText = 'Date of birth is required.'; return false; }

    if (id) {
        // Update existing
        const startIso = new Date(date + 'T' + time).toISOString();
        const payload = {
            doctorId: parseInt(doctorId),
            startTime: startIso,
            durationInMinutes: duration
        };

        const r = await fetch('/api/appointments/' + id, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify(payload)
        });

        if (!r.ok) {
            const txt = await r.text();
            errorEl.innerText = txt || 'Update failed';
            return false;
        }
    } else {
        // Create new - use booking endpoint
        const startIso = new Date(date + 'T' + time).toISOString();
        const payload = {
            doctorId: parseInt(doctorId),
            startTime: startIso,
            durationInMinutes: duration,
            patient: {
                name: patientName,
                phone: phone,
                fileNo: fileNo,
                address: address,
                dateOfBirth: dob,
                gender: gender
            }
        };

        const r = await fetch('/api/appointments', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify(payload)
        });

        if (!r.ok) {
            const txt = await r.text();
            errorEl.innerText = txt || 'Create failed';
            return false;
        }
    }

    return true;
}

async function onDelete(e) {
    if (!confirm('Delete this appointment? This action cannot be undone.')) return;
    const id = e.currentTarget.getAttribute('data-id');
    const r = await fetch('/api/appointments/' + id, {
        method: 'DELETE',
        credentials: 'same-origin'
    });
    if (r.status === 204) {
        loadAppointments();
    } else {
        const txt = await r.text();
        alert('Delete failed: ' + txt);
    }
}

function escapeHtml(s) {
    if (!s) return '';
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

