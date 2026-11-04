// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
    const sidebar = document.getElementById('appSidebar');
    const backdrop = document.getElementById('sidebarBackdrop');
    const toggle = document.getElementById('sidebarToggle');

    const setSidebarOpen = (isOpen) => {
        if (!sidebar || !backdrop || !toggle) return;
        sidebar.classList.toggle('is-open', isOpen);
        backdrop.classList.toggle('is-visible', isOpen);
        toggle.setAttribute('aria-expanded', String(isOpen));
    };

    toggle?.addEventListener('click', () => setSidebarOpen(!sidebar?.classList.contains('is-open')));
    backdrop?.addEventListener('click', () => setSidebarOpen(false));
    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') setSidebarOpen(false);
    });

    document.querySelectorAll('[data-confirm-target]').forEach((trigger) => {
        trigger.addEventListener('click', (event) => {
            const modalElement = document.querySelector(trigger.getAttribute('data-confirm-target'));
            if (!modalElement || !window.bootstrap) return;
            event.preventDefault();
            const modal = window.bootstrap.Modal.getOrCreateInstance(modalElement);
            const confirmButton = modalElement.querySelector('[data-confirm-action]');
            confirmButton?.addEventListener('click', () => {
                const form = trigger.closest('form');
                if (form) form.requestSubmit();
                else if (trigger instanceof HTMLAnchorElement && trigger.href) window.location.assign(trigger.href);
                modal.hide();
            }, { once: true });
            modal.show();
        });
    });

    document.querySelectorAll('.toast[data-autoshow="true"]').forEach((element) => {
        if (window.bootstrap) window.bootstrap.Toast.getOrCreateInstance(element).show();
    });
})();
