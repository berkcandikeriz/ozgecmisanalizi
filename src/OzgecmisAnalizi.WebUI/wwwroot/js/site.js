// ÖzgeçmişAnalizi — küçük, bağımlılıksız etkileşimler.
(function () {
    "use strict";
    const root = document.documentElement;
    root.classList.add("js");

    // ---- Tema (açık / koyu). Tercih yalnızca bu tarayıcıda saklanır.
    const store = {
        get(k) { try { return localStorage.getItem(k); } catch { return null; } },
        set(k, v) { try { localStorage.setItem(k, v); } catch { /* gizli pencere */ } }
    };
    document.querySelectorAll("[data-theme-toggle]").forEach(btn => {
        const sync = () => btn.setAttribute("aria-label", root.dataset.theme === "dark" ? "Açık temaya geç" : "Koyu temaya geç");
        sync();
        btn.addEventListener("click", () => {
            const next = root.dataset.theme === "dark" ? "light" : "dark";
            root.dataset.theme = next;
            store.set("theme", next);
            sync();
        });
    });

    // ---- Mobil menü
    const menuBtn = document.querySelector("[data-menu-toggle]");
    if (menuBtn) {
        menuBtn.addEventListener("click", () => {
            const open = document.body.classList.toggle("nav-open");
            menuBtn.setAttribute("aria-expanded", String(open));
        });
        document.addEventListener("keydown", e => {
            if (e.key === "Escape" && document.body.classList.contains("nav-open")) {
                document.body.classList.remove("nav-open");
                menuBtn.setAttribute("aria-expanded", "false");
                menuBtn.focus();
            }
        });
    }

    // ---- Uyarıları kapatma
    document.addEventListener("click", e => {
        const close = e.target.closest("[data-dismiss]");
        if (close) close.closest(".alert")?.remove();
    });

    // ---- Uzun işlemler: buton yükleniyor durumu (çift gönderimi engeller)
    document.addEventListener("submit", e => {
        const form = e.target;
        if (form.dataset.confirm && !window.confirm(form.dataset.confirm)) {
            e.preventDefault();
            return;
        }
        const btn = e.submitter && e.submitter.matches("[data-loading-text], .btn-primary") ? e.submitter : null;
        if (btn && !form.hasAttribute("data-no-loading") && !btn.hasAttribute("data-no-loading") && !btn.formTarget) {
            setTimeout(() => { btn.dataset.loading = "true"; btn.setAttribute("aria-busy", "true"); }, 0);
        }
    });

    // ---- Seçim değişince formu gönder (kanban aşama)
    document.querySelectorAll("[data-autosubmit]").forEach(el => el.addEventListener("change", () => el.form?.requestSubmit()));

    // ---- Dosya yükleme alanı
    document.querySelectorAll(".dropzone").forEach(zone => {
        const input = zone.querySelector("input[type=file]");
        const name = zone.querySelector(".file-name");
        const show = () => { if (name && input.files.length) name.textContent = input.files[0].name; };
        input.addEventListener("change", show);
        ["dragenter", "dragover"].forEach(t => zone.addEventListener(t, e => { e.preventDefault(); zone.classList.add("is-over"); }));
        ["dragleave", "drop"].forEach(t => zone.addEventListener(t, e => { e.preventDefault(); zone.classList.remove("is-over"); }));
        zone.addEventListener("drop", e => {
            if (e.dataTransfer?.files?.length) { input.files = e.dataTransfer.files; show(); }
        });
        zone.addEventListener("keydown", e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); input.click(); } });
    });

    // ---- Tekrarlanabilir CV bölümleri (deneyim, eğitim, dil...)
    // Her blok: [data-collection="Resume.Content.Experiences"] içinde .repeat-item. Şablon: <template data-for="...">
    document.querySelectorAll("[data-add]").forEach(btn => {
        btn.addEventListener("click", () => {
            const key = btn.dataset.add;
            const list = document.querySelector(`[data-collection="${key}"]`);
            const tpl = document.querySelector(`template[data-for="${key}"]`);
            if (!list || !tpl) return;
            const node = tpl.content.firstElementChild.cloneNode(true);
            list.appendChild(node);
            reindex(list);
            list.closest("form")?.querySelector(`[data-empty-for="${key}"]`)?.remove();
            node.querySelector("input, textarea, select")?.focus();
        });
    });
    document.addEventListener("click", e => {
        const rm = e.target.closest("[data-remove]");
        if (!rm) return;
        const item = rm.closest(".repeat-item");
        const list = item?.parentElement;
        item?.remove();
        if (list) reindex(list);
    });
    function reindex(list) {
        const key = list.dataset.collection;
        const escaped = key.replace(/\./g, "\\.");
        const pattern = new RegExp("^" + escaped + "\\[(\\d+|__i__)\\]");
        [...list.children].forEach((item, i) => {
            item.querySelectorAll("[name]").forEach(el => { el.name = el.name.replace(pattern, `${key}[${i}]`); });
            item.querySelectorAll("[id]").forEach(el => { el.id = el.id.replace(/_(\d+|__i__)_/, `_${i}_`); });
            item.querySelectorAll("label[for]").forEach(el => { el.htmlFor = el.htmlFor.replace(/_(\d+|__i__)_/, `_${i}_`); });
        });
    }
    document.querySelectorAll("form[data-resume-editor]").forEach(form => {
        form.addEventListener("submit", () => form.querySelectorAll("[data-collection]").forEach(reindex));
        // Kaydedilmemiş değişiklik uyarısı
        let dirty = false;
        form.addEventListener("input", () => { dirty = true; });
        form.addEventListener("submit", () => { dirty = false; });
        window.addEventListener("beforeunload", e => { if (dirty) { e.preventDefault(); e.returnValue = ""; } });
    });

    // ---- "Devam ediyor" işaretlenince bitiş tarihini kapat
    document.addEventListener("change", e => {
        const cb = e.target.closest("[data-current-toggle]");
        if (!cb) return;
        const end = cb.closest(".repeat-item")?.querySelector("[data-end-date]");
        if (end) { end.disabled = cb.checked; if (cb.checked) end.value = ""; }
    });

    // ---- Puan halkası: ilk görünümde değere doğru dolar
    document.querySelectorAll(".ring[data-value]").forEach(ring => {
        const circle = ring.querySelector(".value");
        if (!circle) return;
        const r = Number(circle.getAttribute("r"));
        const len = 2 * Math.PI * r;
        const target = len * (1 - Math.min(100, Math.max(0, Number(ring.dataset.value))) / 100);
        circle.style.strokeDasharray = String(len);
        circle.style.strokeDashoffset = String(len);
        requestAnimationFrame(() => requestAnimationFrame(() => { circle.style.strokeDashoffset = String(target); }));
    });

    // ---- Panoya kopyala
    document.querySelectorAll("[data-copy]").forEach(btn => {
        btn.addEventListener("click", async () => {
            const el = document.querySelector(btn.dataset.copy);
            if (!el) return;
            try {
                await navigator.clipboard.writeText(el.value ?? el.textContent);
                const old = btn.textContent;
                btn.textContent = "Kopyalandı";
                setTimeout(() => { btn.textContent = old; }, 1600);
            } catch { el.select?.(); }
        });
    });
})();
