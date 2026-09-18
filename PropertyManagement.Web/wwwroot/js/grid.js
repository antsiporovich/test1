document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    var STATUS_BADGES = {
        'Draft': ['bg-gray-100', 'text-gray-700'],
        'Submitted': ['bg-blue-100', 'text-blue-700'],
        'UnderReview': ['bg-sky-100', 'text-sky-700'],
        'In Review': ['bg-sky-100', 'text-sky-700'],
        'Returned': ['bg-amber-100', 'text-amber-800'],
        'Approved': ['bg-emerald-100', 'text-emerald-800'],
        'Denied': ['bg-red-100', 'text-red-800'],
        'Withdrawn': ['bg-red-100', 'text-red-800']
    };

    var UNIT_IMAGE_SLUGS = ['maple-grove', 'riverside', 'pineview', 'lakeside', 'willow-creek', 'cedar-ridge'];

    function unitImageUrl(propertyName) {
        var name = String(propertyName || '').trim().toLowerCase();
        var known = {
            'maple grove': 'maple-grove',
            'riverside apartments': 'riverside',
            'pineview commons': 'pineview',
            'lakeside flats': 'lakeside',
            'willow creek': 'willow-creek',
            'cedar ridge': 'cedar-ridge'
        };
        var slug = known[name];
        if (!slug) {
            var hash = 0;
            for (var i = 0; i < name.length; i++) { hash = ((hash << 5) - hash) + name.charCodeAt(i); hash |= 0; }
            slug = UNIT_IMAGE_SLUGS[Math.abs(hash) % UNIT_IMAGE_SLUGS.length];
        }
        return '/images/units/' + slug + '.jpg';
    }

    function relativeTime(date) {
        var diffMs = Date.now() - date.getTime();
        var days = Math.floor(diffMs / 86400000);
        if (days < 1) return 'today';
        if (days === 1) return '1 day ago';
        return days + ' days ago';
    }

    function statusBadge(value) {
        // API composes "Under Review (Name)" for the claimed case — show "In Review" and
        // colour by the enum, matching the design's status pills.
        var key = String(value).indexOf('Under Review') === 0 ? 'In Review' : value;
        var label = String(value).indexOf('Under Review') === 0 ? 'In Review' : value;
        var classes = STATUS_BADGES[key] || ['bg-gray-100', 'text-gray-600'];
        var span = document.createElement('span');
        span.className = 'inline-flex items-center px-3 py-1 rounded-full text-[11px] font-semibold ' + classes[0] + ' ' + classes[1];
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
        var showOverflow = root.dataset.gridShowOverflow === 'true';
        var itemLabel = root.dataset.gridItemLabel || 'results';
        var searchInputId = root.dataset.gridSearchInputId || '';
        var fixedParams = {};
        try {
            fixedParams = JSON.parse(root.dataset.gridFixedParams || '{}');
        } catch (e) {
            fixedParams = {};
        }
        var filterInputIds = {};
        try {
            filterInputIds = JSON.parse(root.dataset.gridFilterInputIds || '{}');
        } catch (e) {
            filterInputIds = {};
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

        function twoLineCell(value, opts) {
            opts = opts || {};
            var td = document.createElement('td');
            td.className = 'px-5 py-2.5';
            var parts = String(value).split(' — ');
            var top = document.createElement('p');
            top.className = opts.topClass || 'text-gray-900 font-medium';
            top.textContent = parts[0];
            td.appendChild(top);
            if (parts.length > 1) {
                var sub = document.createElement('p');
                sub.className = 'text-[11px] text-gray-400 mt-0.5';
                sub.textContent = parts.slice(1).join(' — ');
                td.appendChild(sub);
            }
            return td;
        }

        function appPropertyCell(row) {
            var td = document.createElement('td');
            td.className = 'px-5 py-2.5';
            var wrap = document.createElement('div');
            wrap.className = 'flex items-center gap-3';
            var img = document.createElement('img');
            img.src = unitImageUrl(row.propertyName);
            img.alt = row.propertyName || '';
            img.className = 'w-20 h-16 rounded-md object-cover flex-shrink-0 bg-gray-100';
            wrap.appendChild(img);
            var text = document.createElement('div');
            var title = document.createElement('p');
            title.className = 'text-gray-900 font-semibold text-sm';
            title.textContent = row.propertyName || '';
            text.appendChild(title);
            if (row.propertyAddress) {
                var addr = document.createElement('p');
                addr.className = 'text-[11px] text-gray-400 mt-0.5';
                addr.textContent = row.propertyAddress;
                text.appendChild(addr);
            }
            var meta = document.createElement('p');
            meta.className = 'text-[11px] text-gray-400';
            meta.textContent = 'Unit ' + (row.unit || '') + '  |  ' + (row.bedrooms != null ? row.bedrooms : '') + ' bed'
                + (row.bathrooms != null ? '  |  ' + row.bathrooms + ' bath' : '');
            text.appendChild(meta);
            wrap.appendChild(text);
            td.appendChild(wrap);
            return td;
        }

        function dateLineCell(value) {
            var td = document.createElement('td');
            td.className = 'px-5 py-2.5';
            var date = new Date(value);
            var primary = document.createElement('p');
            primary.className = 'text-gray-700';
            primary.textContent = date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
            td.appendChild(primary);
            var sub = document.createElement('p');
            sub.className = 'text-[11px] text-gray-400 mt-0.5';
            sub.textContent = date.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' }) + ' · ' + relativeTime(date);
            td.appendChild(sub);
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
                    if (th.dataset.type === 'appproperty') {
                        tr.appendChild(appPropertyCell(row));
                        return;
                    }
                    if (th.dataset.type === 'twoline') {
                        if (value === null || value === undefined || value === '') {
                            var emptyTd = document.createElement('td');
                            emptyTd.className = 'px-5 py-2.5';
                            emptyTd.innerHTML = '<span class="text-gray-300">—</span>';
                            tr.appendChild(emptyTd);
                        } else {
                            tr.appendChild(twoLineCell(value));
                        }
                        return;
                    }
                    if (th.dataset.type === 'dateline') {
                        tr.appendChild(dateLineCell(value));
                        return;
                    }
                    var td = document.createElement('td');
                    td.className = 'px-5 py-2.5 text-gray-700';
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
                    actionTd.className = 'px-5 py-2.5';
                    var actionsWrap = document.createElement('div');
                    actionsWrap.className = 'flex items-center justify-end gap-2';
                    if (targetUrl) {
                        var link = document.createElement('a');
                        link.href = targetUrl;
                        var isContinue = showOverflow && (row.status === 'Draft' || row.status === 'Returned');
                        if (isContinue) {
                            link.className = 'inline-flex items-center justify-center w-[75px] py-1.5 text-xs font-semibold bg-brand-600 hover:bg-brand-700 text-white rounded-md transition-colors';
                            link.textContent = 'Continue';
                        } else {
                            link.className = 'inline-flex items-center justify-center w-[75px] py-1.5 text-xs font-semibold border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50 transition-colors';
                            link.textContent = rowActionLabel;
                        }
                        actionsWrap.appendChild(link);
                    }
                    if (showOverflow) {
                        var more = document.createElement('button');
                        more.type = 'button';
                        more.className = 'inline-flex items-center justify-center w-8 h-8 border border-gray-300 text-gray-500 hover:bg-gray-50 rounded-md transition-colors';
                        more.setAttribute('aria-label', 'More actions');
                        more.innerHTML = '<i data-lucide="more-horizontal" class="w-4 h-4"></i>';
                        actionsWrap.appendChild(more);
                    }
                    actionTd.appendChild(actionsWrap);
                    tr.appendChild(actionTd);
                }

                body.appendChild(tr);
            });
            if (window.lucide && typeof window.lucide.createIcons === 'function') {
                window.lucide.createIcons();
            }
        }

        function pagerButton(label, opts) {
            opts = opts || {};
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.innerHTML = label;
            if (opts.active) {
                btn.className = 'min-w-[32px] h-8 px-2 flex items-center justify-center text-xs font-semibold rounded-md bg-brand-600 text-white';
            } else {
                btn.className = 'min-w-[32px] h-8 px-2 flex items-center justify-center text-xs font-medium rounded-md border border-gray-300 text-gray-600 hover:bg-gray-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed';
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
                var indicator = th.querySelector('.grid-sort-icon');
                if (!indicator) { return; }
                if (th.dataset.key === state.sort) {
                    indicator.setAttribute('data-lucide', state.desc ? 'chevron-down' : 'chevron-up');
                    indicator.classList.add('text-brand-600');
                    indicator.classList.remove('text-gray-400');
                } else {
                    indicator.setAttribute('data-lucide', 'chevrons-up-down');
                    indicator.classList.remove('text-brand-600');
                    indicator.classList.add('text-gray-400');
                }
            });
            if (window.lucide) { lucide.createIcons(); }
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

        // Page-level filter dropdowns (e.g. Status/Property): fold the changed value
        // into fixedParams and re-fetch in place — no full-page form submit, so the
        // page never jumps back to the top the way onchange="this.form.submit()" would.
        Object.keys(filterInputIds).forEach(function (paramName) {
            var input = document.getElementById(filterInputIds[paramName]);
            if (!input) { return; }
            input.addEventListener('change', function () {
                if (input.value) {
                    fixedParams[paramName] = input.value;
                } else {
                    delete fixedParams[paramName];
                }
                var url = new URL(window.location.href);
                if (input.value) {
                    url.searchParams.set(paramName, input.value);
                } else {
                    url.searchParams.delete(paramName);
                }
                window.history.replaceState(null, '', url);
                state.page = 1;
                load();
            });
        });

        load();
    });
});
