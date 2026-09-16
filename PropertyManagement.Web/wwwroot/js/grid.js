document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    document.querySelectorAll('.grid-component').forEach(function (root) {
        var endpoint = root.dataset.gridEndpoint;
        var pageSize = parseInt(root.dataset.gridPageSize, 10) || 10;
        var rowUrlTemplate = root.dataset.gridRowUrlTemplate || '';
        var fixedParams = {};
        try {
            fixedParams = JSON.parse(root.dataset.gridFixedParams || '{}');
        } catch (e) {
            fixedParams = {};
        }

        var headers = Array.from(root.querySelectorAll('thead th'));
        var body = root.querySelector('.grid-body');
        var summary = root.querySelector('.grid-summary');
        var prevBtn = root.querySelector('.grid-prev');
        var nextBtn = root.querySelector('.grid-next');

        var state = { page: 1, sort: null, desc: false };

        function buildUrl() {
            var params = new URLSearchParams(fixedParams);
            params.set('page', state.page);
            params.set('pageSize', pageSize);
            if (state.sort) {
                params.set('sort', state.sort);
                params.set('desc', state.desc);
            }
            return endpoint + '?' + params.toString();
        }

        function renderRows(rows) {
            body.innerHTML = '';
            if (rows.length === 0) {
                var emptyRow = document.createElement('tr');
                var emptyCell = document.createElement('td');
                emptyCell.colSpan = headers.length;
                emptyCell.className = 'text-muted';
                emptyCell.textContent = 'No results.';
                emptyRow.appendChild(emptyCell);
                body.appendChild(emptyRow);
                return;
            }

            rows.forEach(function (row) {
                var tr = document.createElement('tr');
                if (rowUrlTemplate && row.id !== undefined) {
                    tr.style.cursor = 'pointer';
                    tr.addEventListener('click', function () {
                        window.location.href = rowUrlTemplate.replace('{id}', row.id);
                    });
                }
                headers.forEach(function (th) {
                    var td = document.createElement('td');
                    var value = row[th.dataset.key];
                    if (value === null || value === undefined) {
                        td.textContent = '';
                    } else if (th.dataset.type === 'date') {
                        td.textContent = new Date(value).toLocaleString();
                    } else {
                        td.textContent = value;
                    }
                    tr.appendChild(td);
                });
                body.appendChild(tr);
            });
        }

        function updateSortIndicators() {
            headers.forEach(function (th) {
                var indicator = th.querySelector('.grid-sort-indicator');
                if (!indicator) {
                    return;
                }
                indicator.textContent = th.dataset.key === state.sort ? (state.desc ? '▼' : '▲') : '';
            });
        }

        function load() {
            body.innerHTML = '<tr><td colspan="' + headers.length + '" class="text-muted">'
                + '<span class="spinner-border spinner-border-sm"></span> Loading…</td></tr>';

            fetch(buildUrl(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(function (response) { return response.json(); })
                .then(function (data) {
                    renderRows(data.rows);
                    var totalPages = Math.max(1, Math.ceil(data.totalCount / pageSize));
                    summary.textContent = 'Page ' + state.page + ' of ' + totalPages + ' (' + data.totalCount + ' total)';
                    prevBtn.disabled = state.page <= 1;
                    nextBtn.disabled = state.page >= totalPages;
                    updateSortIndicators();
                });
        }

        headers.forEach(function (th) {
            if (th.dataset.sortable !== 'true') {
                return;
            }
            th.addEventListener('click', function () {
                var key = th.dataset.key;
                if (state.sort === key) {
                    state.desc = !state.desc;
                } else {
                    state.sort = key;
                    state.desc = false;
                }
                state.page = 1;
                load();
            });
        });

        prevBtn.addEventListener('click', function () {
            if (state.page > 1) {
                state.page -= 1;
                load();
            }
        });
        nextBtn.addEventListener('click', function () {
            state.page += 1;
            load();
        });

        load();
    });
});
