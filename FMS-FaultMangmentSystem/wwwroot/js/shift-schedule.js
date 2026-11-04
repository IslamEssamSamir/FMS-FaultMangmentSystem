(() => {
    const panel = document.getElementById('assignmentPanel');
    const form = document.getElementById('assignmentForm');
    const dateInput = document.getElementById('assignmentDate');
    const assignmentId = document.getElementById('assignmentId');
    const departmentSelect = document.getElementById('assignmentDepartment');
    const officerSelect = document.getElementById('assignmentOfficer');
    const definitionSelect = document.getElementById('assignmentDefinition');
    const conflictHint = document.getElementById('conflictHint');

    const openPanel = (date) => {
        if (!panel || !dateInput) return;
        dateInput.value = date;
        panel.classList.add('is-open');
        panel.scrollIntoView({ behavior: 'smooth', block: 'center' });
    };

    document.querySelectorAll('.add-assignment').forEach(button => button.addEventListener('click', () => openPanel(button.dataset.date)));
    document.querySelectorAll('.add-assignment').forEach(button => button.addEventListener('click', () => {
        if (assignmentId) assignmentId.value = '';
        form?.reset();
        dateInput.value = button.dataset.date;
    }));
    document.querySelectorAll('.edit-assignment').forEach(button => button.addEventListener('click', () => {
        openPanel(button.dataset.date);
        if (assignmentId) assignmentId.value = button.dataset.id;
        departmentSelect.value = button.dataset.department;
        loadOfficers(button.dataset.department, button.dataset.user);
        definitionSelect.value = button.dataset.definition;
    }));
    document.getElementById('closeAssignmentPanel')?.addEventListener('click', () => panel?.classList.remove('is-open'));

    async function loadOfficers(departmentId, selectedUserId) {
        if (!officerSelect || !departmentId) return;
        officerSelect.innerHTML = '<option value="">جارٍ تحميل الضباط...</option>';
        try {
            const response = await fetch(`/ShiftSchedules/Officers?departmentId=${encodeURIComponent(departmentId)}`, { headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error('تعذر تحميل قائمة الضباط.');
            const officers = await response.json();
            officerSelect.replaceChildren(new Option('اختر الضابط', ''));
            officers.forEach(officer => officerSelect.add(new Option(officer.fullName, officer.id, false, officer.id === selectedUserId)));
            if (officers.length === 0) officerSelect.add(new Option('لا يوجد ضباط مؤهلون في هذا القسم', ''));
        } catch {
            officerSelect.innerHTML = '<option value="">تعذر تحميل الضباط</option>';
        }
    }

    departmentSelect?.addEventListener('change', () => loadOfficers(departmentSelect.value));
    definitionSelect?.addEventListener('change', () => {
        if (conflictHint) {
            conflictHint.classList.toggle('is-warning', Boolean(definitionSelect.value));
            conflictHint.textContent = 'سيتم التحقق من تعارض الفترات الزمنية الفعلية عند الحفظ.';
        }
    });
    form?.addEventListener('submit', event => {
        if (!form.reportValidity()) event.preventDefault();
    });
})();
