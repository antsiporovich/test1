document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    var STATUS_BADGES = {
        'Draft': ['bg-gray-100', 'text-gray-600'],
        'Submitted': ['bg-emerald-50', 'text-emerald-700'],
        'UnderReview': ['bg-sky-50', 'text-sky-700'],
        'In Review': ['bg-sky-50', 'text-sky-700'],
        'Returned': ['bg-amber-50', 'text-amber-700'],
        'Approved': ['bg-green-100', 'text-green-800'],
        'Denied': ['bg-red-50', 'text-red-700'],
        'Withdrawn': ['bg-gray-100', 'text-gray-500']
    };

    function statusBadge(value) {
        // API composes "Under Review (Name)" for the claimed case — show "In Review" and
        // colour by the enum, matching the design's status pills.
        var key = String(value).indexOf('Under Review') === 0 ? 'In Review' : value;
        var label = String(value).indexOf('Under Review') === 0 ? 'In Review' : value;
        var classes = STATUS_BADGES[key] || ['bg-gray-100', 'text-gray-600'];
        var span = document.createElement('span');
        span.className = 'inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold ' + classes[0] + ' ' + classes[1];
        span.textContent = label;
        return span;
    }

    function debounce(fn, wait) {
        var t;
        return function () {
            var args = arguments;
            clearTimeout(t);
            t = setTimeout(function () { fn.apply(null, args); }, wait);
        };
    }

    document.querySelectorAll('.grid-component').forEach(function (root) {
        var endpoint = root.dataset.gridEndpoint;
        var pageSize = parseInt(root.dataset.gridPageSize, 10) || 10;
        var rowUrlTemplate = root.dataset.gridRowUrlTemplate || '';
        var rowActionLabel = root.dataset.gridRowActionLabel || '';
        var itemLabel = root.dataset.gridItemLabel || 'results';
        var searchInputId = root.dataset.gridSearchInputId || '';
        var fixedParams = {};
        try {
            fixedParams = JSON.parse(root.dataset.gridFixedParams || '{}');
        } catch (e) {
            fixedParams = {};
        }

        var headers = Array.from(root.querySelectorAll('thead th[data-key]'));
        var colCount = root.querySelectorAll('thead th').length;
        var body = root.querySelector('.grid-body');
        var tableWrap = root.querySelector('.grid-table-wrap');
        var summary = root.querySelector('.grid-summary');
        var pager = root.querySelector('.grid-pager');

        var state = { page: 1, sort: null, desc: false, search: '', total: 0 };

        function buildUrl() {
            var params = new URLSearchParams(fixedParams);
            params.set('page', state.page);
            params.set('pageSize', pageSize);
            if (state.sort) {
                params.set('sort', state.sort);
                params.set('desc', state.desc);
            }
            if (state.search) {
                params.set('search', state.search);
            }
            return endpoint + '?' + params.toString();
        }

        function twoLineCell(value) {
            var td = document.createElement('td');
            td.className = 'px-6 py-2.5';
            var parts = String(value).split(' — ');
            var top = document.createElement('div');
            top.className = 'text-gray-900 font-medium';
            top.textContent = parts[0];
            td.appendChild(top);
            if (parts.length > 1) {
                var sub = document.createElement('div');
                sub.className = 'text-[11px] text-gray-400 mt-0.5';
                sub.textContent = parts.slice(1).join(' — ');
                td.appendChild(sub);
            }
            return td;
        }

        function renderRows(rows) {
            body.innerHTML = '';
            if (rows.length === 0) {
                var emptyRow = document.createElement('tr');
                var emptyCell = document.createElement('td');
                emptyCell.colSpan = colCount;
                emptyCell.className = 'px-6 py-10 text-center text-sm text-gray-400';
                emptyCell.textContent = 'No results.';
                emptyRow.appendChild(emptyCell);
                body.appendChild(emptyRow);
                return;
            }

            rows.forEach(function (row) {
                var tr = document.createElement('tr');
                tr.className = 'border-b border-gray-100 last:border-0 hover:bg-gray-50/70 transition-colors';
                var targetUrl = (rowUrlTemplate && row.id !== undefined) ? rowUrlTemplate.replace('{id}', row.id) : null;

                // Row click only when there is no explicit action button (avoids double affordance).
                if (targetUrl && !rowActionLabel) {
                    tr.style.cursor = 'pointer';
                    tr.addEventListener('click', function () { window.location.href = targetUrl; });
                }

                headers.forEach(function (th) {
                    var value = row[th.dataset.key];
                    if (th.dataset.type === 'twoline') {
                        tr.appendChild(twoLineCell(value));
                        return;
                    }
                    var td = document.createElement('td');
                    td.className = 'px-6 py-2.5 text-gray-700';
                    if (value === null || value === undefined || value === '') {
                        td.innerHTML = '<span class="text-gray-300">—</span>';
                    } else if (th.dataset.type === 'date') {
                        td.className += ' whitespace-nowrap';
                        td.textContent = new Date(value).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
                    } else if (th.dataset.type === 'status') {
                        td.appendChild(statusBadge(value));
                    } else {
                        td.textContent = value;
                    }
                    tr.appendChild(td);
                });

                if (rowActionLabel) {
                    var actionTd = document.createElement('td');
                    actionTd.className = 'px-6 py-2.5 text-right';
                    if (targetUrl) {
                        var link = document.createElement('a');
                        link.href = targetUrl;
                        link.className = 'inline-flex items-center justify-center w-[75px] py-1.5 text-xs font-medium border border-gray-300 rounded-md text-gray-600 hover:bg-gray-50 transition-colors';
                        link.textContent = rowActionLabel;
                        actionTd.appendChild(link);
                    }
                    tr.appendChild(actionTd);
                }

                body.appendChild(tr);
            });
        }

        function pagerButton(label, opts) {
            opts = opts || {};
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.innerHTML = label;
            if (opts.active) {
                btn.className = 'min-w-[32px] h-8 px-2 flex items-center justify-center text-sm font-semibold rounded-md bg-brand-600 text-white';
            } else {
                btn.className = 'min-w-[32px] h-8 px-2 flex items-center justify-center text-sm font-medium rounded-md border border-gray-300 text-gray-600 hover:bg-gray-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed';
            }
            if (opts.disabled) { btn.disabled = true; }
            if (opts.onClick && !opts.disabled && !opts.active) { btn.addEventListener('click', opts.onClick); }
            return btn;
        }

        function renderPager(totalPages) {
            pager.innerHTML = '';
            if (totalPages <= 1) { return; }

            pager.appendChild(pagerButton('&lsaquo;', {
                disabled: state.page <= 1,
                onClick: function () { state.page -= 1; load(); }
            }));

            var pages = [];
            if (totalPages <= 7) {
                for (var i = 1; i <= totalPages; i++) { pages.push(i); }
            } else {
                pages.push(1);
                var start = Math.max(2, state.page - 1);
                var end = Math.min(totalPages - 1, state.page + 1);
                if (start > 2) { pages.push('…'); }
                for (var p = start; p <= end; p++) { pages.push(p); }
                if (end < totalPages - 1) { pages.push('…'); }
                pages.push(totalPages);
            }

            pages.forEach(function (p) {
                if (p === '…') {
                    var span = document.createElement('span');
                    span.className = 'px-1 text-sm text-gray-400';
                    span.textContent = '…';
                    pager.appendChild(span);
                    return;
                }
                pager.appendChild(pagerButton(String(p), {
                    active: p === state.page,
                    onClick: function () { state.page = p; load(); }
                }));
            });

            pager.appendChild(pagerButton('&rsaquo;', {
                disabled: state.page >= totalPages,
                onClick: function () { state.page += 1; load(); }
            }));
        }

        function updateSortIndicators() {
            headers.forEach(function (th) {
                var indicator = th.querySelector('.grid-sort-indicator');
                if (!indicator) { return; }
                indicator.textContent = th.dataset.key === state.sort ? (state.desc ? '▼' : '▲') : '';
            });
        }

        function renderSummary() {
            if (state.total === 0) {
                summary.textContent = 'Showing 0 ' + itemLabel;
                return;
            }
            var start = (state.page - 1) * pageSize + 1;
            var end = Math.min(state.page * pageSize, state.total);
            summary.textContent = 'Showing ' + start + '–' + end + ' of ' + state.total + ' ' + itemLabel;
        }

        function load() {
            // Hold the current table height while swapping in the loading row so the page
            // does not "jump" up (and back) as the body briefly collapses on reload.
            if (tableWrap && tableWrap.offsetHeight > 0) {
                tableWrap.style.minHeight = tableWrap.offsetHeight + 'px';
            }
            body.innerHTML = '<tr><td colspan="' + colCount + '" class="px-6 py-8 text-center text-sm text-gray-400">Loading…</td></tr>';

            fetch(buildUrl(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(function (response) { return response.json(); })
                .then(function (data) {
                    state.total = data.totalCount;
                    renderRows(data.rows);
                    var totalPages = Math.max(1, Math.ceil(data.totalCount / pageSize));
                    renderSummary();
                    renderPager(totalPages);
                    updateSortIndicators();
                    if (tableWrap) { tableWrap.style.minHeight = ''; }
                });
        }

        headers.forEach(function (th) {
            if (th.dataset.sortable !== 'true') { return; }
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

        if (searchInputId) {
            var searchInput = document.getElementById(searchInputId);
            if (searchInput) {
                searchInput.addEventListener('input', debounce(function () {
                    state.search = searchInput.value.trim();
                    state.page = 1;
                    load();
                }, 300));
            }
        }

        load();
    });
});
