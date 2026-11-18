(function () {
    "use strict";

    // Mobile sidebar drawer toggle
    const sidebar = document.getElementById("smsSidebar");
    const backdrop = document.getElementById("smsSidebarBackdrop");
    const toggleBtn = document.getElementById("sidebarToggle");

    function closeSidebar() {
        if (!sidebar || !backdrop) return;
        sidebar.classList.remove("open");
        backdrop.classList.remove("open");
        if (toggleBtn) toggleBtn.setAttribute("aria-expanded", "false");
    }

    if (toggleBtn && sidebar && backdrop) {
        toggleBtn.addEventListener("click", function () {
            const isOpen = sidebar.classList.toggle("open");
            backdrop.classList.toggle("open", isOpen);
            toggleBtn.setAttribute("aria-expanded", isOpen ? "true" : "false");
        });
        backdrop.addEventListener("click", closeSidebar);
    }

    // Sidebar user profile dropdown
    const profileBtn = document.getElementById("profileMenuToggle");
    const profileMenu = document.getElementById("profileMenu");

    function closeProfileMenu() {
        if (!profileBtn || !profileMenu) return;
        profileMenu.classList.remove("open");
        profileBtn.setAttribute("aria-expanded", "false");
    }

    if (profileBtn && profileMenu) {
        profileBtn.addEventListener("click", function (e) {
            e.stopPropagation();
            const isOpen = profileMenu.classList.toggle("open");
            profileBtn.setAttribute("aria-expanded", isOpen ? "true" : "false");
        });

        document.addEventListener("click", function (e) {
            if (!profileMenu.contains(e.target) && !profileBtn.contains(e.target)) {
                closeProfileMenu();
            }
        });
    }

    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") {
            closeSidebar();
            closeProfileMenu();
        }
    });

    // Dashboard Chart Series Switcher (Primary vs Secondary) + Legend Sync
    const switchers = document.querySelectorAll("select[data-chart-switch]");
    switchers.forEach(function (sel) {
        sel.addEventListener("change", function () {
            const panel = sel.closest(".sms-panel");
            if (!panel) return;
            const targetKey = sel.value;
            const panels = panel.querySelectorAll("[data-chart-panel]");
            panels.forEach(function (p) {
                p.classList.toggle("d-none", p.getAttribute("data-chart-panel") !== targetKey);
            });

            const opt = sel.options[sel.selectedIndex];
            if (opt) {
                const pLeg = opt.getAttribute("data-legend-primary");
                const sLeg = opt.getAttribute("data-legend-secondary");
                const pSlot = panel.querySelector('[data-legend-slot="primary"]');
                const sSlot = panel.querySelector('[data-legend-slot="secondary"]');
                if (pLeg && pSlot) pSlot.textContent = pLeg;
                if (sLeg && sSlot) sSlot.textContent = sLeg;
            }
        });
    });

    // Interactive Area Chart Node Tooltips (pure SVG attributes, CSP-safe)
    const areaSvgs = document.querySelectorAll("svg[data-area-svg]");
    areaSvgs.forEach(function (svg) {
        const pill = svg.querySelector(".sms-peak-pill");
        const pillBg = svg.querySelector(".sms-peak-pill-bg");
        const pillText = svg.querySelector(".sms-peak-pill-text");
        const guide = svg.querySelector(".sms-peak-guide");
        const hits = svg.querySelectorAll(".sms-node-hit");

        if (!pill || !pillText || !guide || hits.length === 0) return;

        function activateHit(hit) {
            const x = parseFloat(hit.getAttribute("data-cx") || hit.getAttribute("cx") || "0");
            const y = parseFloat(hit.getAttribute("data-cy") || hit.getAttribute("cy") || "0");
            const val = hit.getAttribute("data-value") || "";

            const boxW = Math.max(48, val.length * 7.2 + 18);
            const bx = Math.max(76, Math.min(468, x));
            const by = Math.max(4, y - 30);

            if (pillBg) {
                pillBg.setAttribute("x", (-boxW / 2).toFixed(1));
                pillBg.setAttribute("width", boxW.toFixed(1));
            }
            pill.setAttribute("transform", "translate(" + bx.toFixed(1) + ", " + by.toFixed(1) + ")");
            pillText.textContent = val;

            guide.setAttribute("x1", x.toFixed(1));
            guide.setAttribute("x2", x.toFixed(1));
            guide.setAttribute("y1", y.toFixed(1));
        }

        hits.forEach(function (hit) {
            ["mouseenter", "mouseover", "pointerenter", "focus", "click"].forEach(function (evt) {
                hit.addEventListener(evt, function () { activateHit(hit); });
            });
        });
    });

    // Interactive Bar Chart Hover Tooltip (pure SVG attributes, CSP-safe)
    const barSvgs = document.querySelectorAll("svg[data-bar-svg]");
    barSvgs.forEach(function (svg) {
        const tip = svg.querySelector(".sms-bar-tip");
        const tipBg = svg.querySelector(".sms-bar-tip-bg");
        const tipText = svg.querySelector(".sms-bar-tip-text");
        const bars = svg.querySelectorAll("rect[data-tip-text]");

        if (!tip || !tipBg || !tipText || bars.length === 0) return;

        function showBarTip(bar) {
            const bx = parseFloat(bar.getAttribute("x") || "0");
            const bw = parseFloat(bar.getAttribute("width") || "14");
            const by = parseFloat(bar.getAttribute("y") || "0");
            const text = bar.getAttribute("data-tip-text") || "";

            const w = Math.max(66, text.length * 6.8 + 18);
            const tx = Math.max(w / 2 + 8, Math.min(532 - w / 2, bx + bw / 2));
            const ty = Math.max(4, by - 28);

            tipBg.setAttribute("x", (-w / 2).toFixed(1));
            tipBg.setAttribute("width", w.toFixed(1));
            tipText.textContent = text;
            tip.setAttribute("transform", "translate(" + tx.toFixed(1) + ", " + ty.toFixed(1) + ")");
            tip.classList.remove("d-none");
        }

        bars.forEach(function (bar) {
            ["mouseenter", "mouseover", "pointerenter", "click"].forEach(function (evt) {
                bar.addEventListener(evt, function () { showBarTip(bar); });
            });
            bar.addEventListener("mouseleave", function () {
                tip.classList.add("d-none");
            });
        });
    });

    // Prevent duplicate form POSTs (e.g., double-clicking Login before 302 redirect finishes)
    const singleSubmitForms = document.querySelectorAll('form[data-single-submit="true"]');
    singleSubmitForms.forEach(function (form) {
        const submitBtn = form.querySelector('button[type="submit"], input[type="submit"]');
        const defaultLabel = submitBtn
            ? (submitBtn.tagName === "INPUT" ? submitBtn.value : submitBtn.textContent)
            : "";

        function resetSubmitState() {
            delete form.dataset.submitting;
            if (!submitBtn) return;
            submitBtn.disabled = false;
            if (submitBtn.tagName === "INPUT") {
                submitBtn.value = defaultLabel;
            } else {
                submitBtn.textContent = defaultLabel;
            }
        }

        form.addEventListener("submit", function (e) {
            if (form.dataset.submitting === "true") {
                e.preventDefault();
                return;
            }

            if (window.jQuery && window.jQuery.fn && window.jQuery.fn.valid) {
                if (!window.jQuery(form).valid()) {
                    return;
                }
            } else if (!form.checkValidity()) {
                return;
            }

            form.dataset.submitting = "true";
            if (submitBtn) {
                const pendingText = submitBtn.getAttribute("data-submitting-text");
                submitBtn.disabled = true;
                if (pendingText) {
                    if (submitBtn.tagName === "INPUT") {
                        submitBtn.value = pendingText;
                    } else {
                        submitBtn.textContent = pendingText;
                    }
                }
            }
        });

        window.addEventListener("pageshow", function (ev) {
            if (ev.persisted || form.dataset.submitting === "true") {
                resetSubmitState();
            }
        });
    });
})();
