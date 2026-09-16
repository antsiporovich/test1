document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    var modalEl = document.getElementById('ajaxModal');
    var modalBody = document.getElementById('ajaxModalBody');
    if (!modalEl || !modalBody || !window.bootstrap) {
        return;
    }
    var bsModal = new bootstrap.Modal(modalEl);

    function getAntiForgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function parseUnobtrusiveValidation(container) {
        if (window.jQuery && jQuery.validator && jQuery.validator.unobtrusive) {
            jQuery.validator.unobtrusive.parse(container);
        }
    }

    function refreshRegion(url, targetSelector) {
        if (!url || !targetSelector) {
            return Promise.resolve();
        }
        return fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (response) { return response.text(); })
            .then(function (html) {
                var target = document.querySelector(targetSelector);
                if (target) {
                    target.innerHTML = html;
                }
            });
    }

    function openModal(url, refreshUrl, refreshTarget) {
        modalEl.dataset.refreshUrl = refreshUrl || '';
        modalEl.dataset.refreshTarget = refreshTarget || '';

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (response) { return response.text(); })
            .then(function (html) {
                modalBody.innerHTML = html;
                parseUnobtrusiveValidation(modalBody);
                bsModal.show();
            });
    }

    document.addEventListener('click', function (e) {
        var opener = e.target.closest('[data-ajax-modal-url]');
        if (opener) {
            e.preventDefault();
            openModal(opener.dataset.ajaxModalUrl, opener.dataset.refreshUrl, opener.dataset.refreshTarget);
            return;
        }

        var remover = e.target.closest('[data-ajax-remove-url]');
        if (remover) {
            e.preventDefault();
            var confirmMessage = remover.dataset.confirm || 'Are you sure?';
            if (!window.confirm(confirmMessage)) {
                return;
            }

            var body = new URLSearchParams();
            body.set('__RequestVerificationToken', getAntiForgeryToken());

            fetch(remover.dataset.ajaxRemoveUrl, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: body
            }).then(function (response) {
                if (!response.ok) {
                    return response.json().then(function (data) {
                        throw new Error(data.message || 'The action could not be completed.');
                    });
                }
                return refreshRegion(remover.dataset.refreshUrl, remover.dataset.refreshTarget);
            }).catch(function (err) {
                window.alert(err.message);
            });
        }
    });

    modalBody.addEventListener('submit', function (e) {
        var form = e.target;
        if (form.tagName !== 'FORM') {
            return;
        }
        e.preventDefault();

        fetch(form.action, {
            method: form.method || 'POST',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            body: new FormData(form)
        }).then(function (response) {
            var contentType = response.headers.get('content-type') || '';
            if (contentType.indexOf('application/json') !== -1) {
                return response.json().then(function (data) {
                    if (data.success) {
                        bsModal.hide();
                        return refreshRegion(modalEl.dataset.refreshUrl, modalEl.dataset.refreshTarget);
                    }
                });
            }
            return response.text().then(function (html) {
                modalBody.innerHTML = html;
                parseUnobtrusiveValidation(modalBody);
            });
        });
    });
});
