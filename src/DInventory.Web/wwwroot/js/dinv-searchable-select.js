/*!
 * DinvSearchableSelect - minimal, dependency-free "type to filter" wrapper around a plain
 * <select> that already has all its <option>s rendered (server-side, same as before). No AJAX,
 * no external library (there's no jQuery in this project) - just hides the real <select> and
 * layers a text input + filtered list on top of it, so every existing bit of code that reads
 * select.value / select.selectedIndex / option.dataset.* keeps working completely unchanged.
 *
 * Usage:
 *   const widget = DinvSearchableSelect.make('variantSelect', { placeholder: 'Search...' });
 *   widget.refreshDisplay(); // call after any code sets select.value directly (e.g. select.value = '')
 */
(function (global) {
    'use strict';

    function make(selectId, options) {
        var opts = options || {};
        var placeholderText = opts.placeholder || 'Type to search...';
        var maxResults = opts.maxResults || 50;

        var select = document.getElementById(selectId);
        if (!select) return null;

        var wrapper = document.createElement('div');
        wrapper.className = 'dinv-searchable-select position-relative';

        var input = document.createElement('input');
        input.type = 'text';
        input.className = select.className || 'form-select';
        input.placeholder = placeholderText;
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('aria-label', placeholderText);

        var menu = document.createElement('div');
        menu.className = 'dinv-searchable-menu list-group shadow-sm';
        menu.style.cssText = 'position:absolute; z-index:1050; max-height:260px; overflow-y:auto; display:none; top:100%; left:0; right:0;';

        // Hide (not remove) the original select - it stays the single source of truth for
        // value/selectedIndex/dataset, exactly like before this widget existed.
        select.style.display = 'none';
        select.parentNode.insertBefore(wrapper, select);
        wrapper.appendChild(input);
        wrapper.appendChild(menu);
        wrapper.appendChild(select);

        function optionEntries() {
            return Array.prototype.slice.call(select.options).filter(function (o) { return o.value !== ''; });
        }

        function currentLabel() {
            var opt = select.options[select.selectedIndex];
            return opt && opt.value !== '' ? opt.textContent : '';
        }

        function closeMenu() {
            menu.style.display = 'none';
        }

        function renderMenu(query) {
            var q = (query || '').trim().toLowerCase();
            var all = optionEntries();
            var matches = all.filter(function (o) {
                return !q || o.textContent.toLowerCase().indexOf(q) !== -1;
            }).slice(0, maxResults);

            if (matches.length === 0) {
                menu.innerHTML = '<div class="list-group-item text-muted small">No matches</div>';
            } else {
                menu.innerHTML = matches.map(function (o) {
                    return '<button type="button" class="list-group-item list-group-item-action small" data-value="' +
                        o.value.replace(/"/g, '&quot;') + '">' + o.textContent + '</button>';
                }).join('') + (all.length > matches.length
                    ? '<div class="list-group-item text-muted small">' + (all.length - matches.length) + ' more - keep typing to narrow down</div>'
                    : '');
            }
            menu.style.display = 'block';
        }

        input.addEventListener('focus', function () {
            renderMenu(input.value === currentLabel() ? '' : input.value);
        });
        input.addEventListener('input', function () {
            renderMenu(input.value);
        });
        // Delay so a click on a menu item (mousedown -> then blur -> then click) still registers
        // before the menu disappears.
        input.addEventListener('blur', function () {
            setTimeout(closeMenu, 150);
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeMenu();
                input.blur();
            }
        });

        menu.addEventListener('mousedown', function (e) {
            var btn = e.target.closest('[data-value]');
            if (!btn) return;
            e.preventDefault();
            select.value = btn.dataset.value;
            input.value = currentLabel();
            closeMenu();
            select.dispatchEvent(new Event('change', { bubbles: true }));
        });

        input.value = currentLabel();

        return {
            /** Re-syncs the visible text from the real select - call this after any other code
             * sets select.value directly (e.g. clearing it back to the placeholder option). */
            refreshDisplay: function () {
                input.value = currentLabel();
            },
            reset: function () {
                select.value = '';
                input.value = '';
                select.dispatchEvent(new Event('change', { bubbles: true }));
            }
        };
    }

    global.DinvSearchableSelect = { make: make };
})(window);
