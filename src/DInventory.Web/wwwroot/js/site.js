// ---- Currency (BDT / Taka) ------------------------------------------------
// This is a Bangladeshi business - every price/amount shown via JS is formatted in Taka, matching
// the server-side MoneyExtensions.ToMoney() helper used in Razor views (currency symbol + thousands
// separators + 2 decimal places), never a "$" sign.
function dinvMoney(value) {
    const n = Number(value) || 0;
    return '\u09F3' + n.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

// Sidebar toggle (mobile)
document.addEventListener('DOMContentLoaded', () => {
    const toggle = document.getElementById('sidebarToggle');
    const sidebar = document.getElementById('erpSidebar');
    if (toggle && sidebar) {
        toggle.addEventListener('click', () => sidebar.classList.toggle('show'));
        document.addEventListener('click', (e) => {
            if (sidebar.classList.contains('show') && !sidebar.contains(e.target) && e.target !== toggle) {
                sidebar.classList.remove('show');
            }
        });
    }
});

// ---- Grouped sidebar nav (Admin / Sales / Reports / Catalog collapse) -----
// Implemented with plain DOM APIs instead of Bootstrap's data-bs-toggle="collapse" component, so the
// group headers always expand/collapse even if the Bootstrap JS bundle is slow, blocked, or fails to
// load from its CDN - only the CSS from bootstrap.min.css (which paints the sidebar itself) is relied
// on here, not bootstrap.bundle.min.js.
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.erp-nav-group-toggle').forEach((toggle) => {
        toggle.addEventListener('click', (e) => {
            e.preventDefault();
            const targetId = toggle.getAttribute('data-nav-target');
            const target = targetId && document.getElementById(targetId);
            if (!target) return;

            const isShown = target.classList.toggle('show');
            toggle.classList.toggle('collapsed', !isShown);
            toggle.setAttribute('aria-expanded', isShown ? 'true' : 'false');
        });
    });
});

// ---- Auth cookie helpers -------------------------------------------------
function getCookie(name) {
    const match = document.cookie.match('(^|;)\\s*' + name + '\\s*=\\s*([^;]+)');
    return match ? decodeURIComponent(match.pop()) : null;
}

/**
 * Fetch wrapper that attaches the JWT access token as a Bearer header and
 * transparently refreshes it once on a 401 before retrying the request.
 * Use this for calls to the /api/* JWT-secured endpoints (e.g. dashboard charts).
 */
async function dinvFetch(url, options = {}) {
    options.headers = options.headers || {};
    const token = getCookie('dinv_access_token');
    if (token) {
        options.headers['Authorization'] = 'Bearer ' + token;
    }

    let response = await fetch(url, options);

    if (response.status === 401) {
        const refreshed = await refreshAccessToken();
        if (refreshed) {
            const newToken = getCookie('dinv_access_token');
            if (newToken) {
                options.headers['Authorization'] = 'Bearer ' + newToken;
            }
            response = await fetch(url, options);
        }
    }

    return response;
}

async function refreshAccessToken() {
    try {
        const response = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' });
        return response.ok;
    } catch {
        return false;
    }
}

// Proactively refresh the access token every 20 minutes while the tab is open
// (access tokens default to a 30 minute lifetime - see appsettings.json "Jwt:AccessTokenMinutes").
if (document.body && document.body.classList.contains('is-authenticated') === false) {
    // no-op placeholder; refresh timer below runs regardless, refresh endpoint is a no-op if not logged in
}

setInterval(() => {
    if (getCookie('dinv_access_token')) {
        refreshAccessToken();
    }
}, 20 * 60 * 1000);

// ---- Barcode scanner support ---------------------------------------------
// This business's barcode scanners are keyboard-wedge devices: they "type" the barcode's characters
// very fast and then send an Enter keypress. Any <input class="barcode-scan-input"> gets two things
// for free: (1) pressing Enter never submits the surrounding <form> by accident - callers that DO
// want an action on Enter should listen for the 'dinv:barcode-scanned' CustomEvent instead, and
// (2) a data-autofocus="true" input is refocused automatically after every scan so the next scan can
// be fired immediately without the cashier/clerk touching the mouse or keyboard.
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.barcode-scan-input').forEach((input) => {
        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                const barcode = input.value.trim();
                if (barcode.length > 0) {
                    input.dispatchEvent(new CustomEvent('dinv:barcode-scanned', { bubbles: true, detail: { barcode } }));
                }
            }
        });
    });
});

/**
 * Refocuses a barcode-scan input once, immediately. Call this right after handling a scan (or once
 * on page load) so the next scan can fire without the user touching the mouse/keyboard - unlike the
 * old dinvKeepFocused helper this used to be, it does NOT hijack focus on every click anywhere on the
 * page, which made every other field (quantity, customer picker, discount/tax, submit button)
 * unusable on the Sales/POS screen. Pages that want "always focused unless the user is clearly using
 * another field" can call this from their own blur handler instead of relying on a global listener.
 */
function dinvRefocusScanInput(inputEl) {
    if (inputEl) {
        inputEl.focus();
    }
}
