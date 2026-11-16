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

    // ---- Mobil menü: sağdan açılan çekmece
    const menuBtn = document.querySelector("[data-menu-toggle]");
    const drawer = document.getElementById("site-nav");
    if (menuBtn && drawer) {
        const setOpen = open => {
            document.body.classList.toggle("nav-open", open);
            menuBtn.setAttribute("aria-expanded", String(open));
            if (open) {
                drawer.querySelector("[data-menu-close]")?.focus();
            } else {
                menuBtn.focus();
            }
        };
        menuBtn.addEventListener("click", () => setOpen(!document.body.classList.contains("nav-open")));
        document.querySelectorAll("[data-menu-close]").forEach(el => el.addEventListener("click", () => setOpen(false)));
        drawer.querySelectorAll("a").forEach(a => a.addEventListener("click", () => document.body.classList.remove("nav-open")));
        document.addEventListener("keydown", e => {
            if (e.key === "Escape" && document.body.classList.contains("nav-open")) setOpen(false);
        });
        // Masaüstü genişliğine dönülürse çekmece açık kalmasın.
        window.matchMedia("(min-width: 901px)").addEventListener("change", e => { if (e.matches) document.body.classList.remove("nav-open"); });
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
    // ---- PDF önizlemesi: editör formunun o anki hâli (kaydetmeden) seçili şablonla sayfa görsellerine çevrilir.
    document.querySelectorAll("[data-preview]").forEach(panel => {
        const form = document.querySelector("form[data-resume-editor]");
        if (!form) return;
        const pages = panel.querySelector("[data-preview-pages]");
        const count = panel.querySelector("[data-preview-count]");
        const status = panel.querySelector("[data-preview-status]");
        const more = panel.querySelector("[data-preview-more]");
        let timer = null, controller = null;

        const refresh = async () => {
            controller?.abort();
            controller = new AbortController();
            form.querySelectorAll("[data-collection]").forEach(reindex);
            const data = new FormData(form);
            data.delete("next");
            panel.setAttribute("aria-busy", "true");
            status.textContent = "Önizleme hazırlanıyor…";
            try {
                const res = await fetch(panel.dataset.preview, { method: "POST", body: data, signal: controller.signal, headers: { "X-Requested-With": "fetch" } });
                const json = await res.json();
                if (!res.ok) throw new Error(json.error || "Önizleme oluşturulamadı.");
                count.textContent = json.pageCount + " sayfa";
                pages.replaceChildren(...json.pages.map((src, i) => {
                    const fig = document.createElement("figure");
                    const img = document.createElement("img");
                    img.src = src;
                    img.alt = (i + 1) + ". sayfa önizlemesi";
                    img.loading = "lazy";
                    const cap = document.createElement("figcaption");
                    cap.textContent = (i + 1) + " / " + json.pageCount;
                    fig.append(img, cap);
                    return fig;
                }));
                more.hidden = json.pageCount <= json.pages.length;
                more.textContent = more.hidden ? "" : `İlk ${json.pages.length} sayfa gösteriliyor. Tamamı için PDF'i indirin.`;
                status.textContent = json.pageCount > 2
                    ? "CV 2 sayfayı aşıyor; işe alımcılar ve ATS için 1–2 sayfa idealdir."
                    : "Seçili şablonla PDF'in sayfaları; kaydetmeden günceller.";
            } catch (err) {
                if (err.name === "AbortError") return;
                status.textContent = err instanceof SyntaxError
                    ? "Önizleme alınamadı; oturumunuz sona ermiş olabilir. Sayfayı yenileyin."
                    : (err.message || "Önizleme oluşturulamadı.");
            } finally {
                panel.removeAttribute("aria-busy");
            }
        };
        const schedule = delay => { clearTimeout(timer); timer = setTimeout(refresh, delay); };

        // Şablon değişince hemen, yazarken 1,2 sn duraklamadan sonra güncelle.
        form.addEventListener("change", e => schedule(e.target.name === "Resume.Template" ? 50 : 600));
        form.addEventListener("input", () => schedule(1200));
        form.addEventListener("click", e => { if (e.target.closest("button[type=button]")) schedule(400); });
        schedule(0);
    });

    // ---- Vesikalık fotoğraf: tarayıcıda 3:4 kırpılır, 300×400 JPEG'e küçültülür ve gizli alana data URL olarak yazılır.
    document.querySelectorAll("[data-photo-field]").forEach(field => {
        const input = field.querySelector("[data-photo-input]");
        const value = field.querySelector("[data-photo-value]");
        const preview = field.querySelector("[data-photo-preview]");
        const empty = field.querySelector("[data-photo-empty]");
        const remove = field.querySelector("[data-photo-remove]");
        const error = field.querySelector("[data-photo-error]");
        const show = url => {
            value.value = url || "";
            preview.hidden = !url;
            if (url) preview.src = url; else preview.removeAttribute("src");
            empty.hidden = !!url;
            remove.hidden = !url;
            value.dispatchEvent(new Event("input", { bubbles: true }));
        };
        const fail = msg => { error.textContent = msg; error.hidden = false; };
        input.addEventListener("change", () => {
            error.hidden = true;
            const file = input.files && input.files[0];
            input.value = "";
            if (!file) return;
            if (!/^image\/(jpeg|png)$/.test(file.type)) { fail("Yalnızca JPEG veya PNG seçin."); return; }
            if (file.size > 15 * 1024 * 1024) { fail("Dosya 15 MB'tan büyük."); return; }
            const img = new Image();
            const src = URL.createObjectURL(file);
            img.onload = () => {
                const W = 300, H = 400;
                const ratio = W / H;
                let sw = img.naturalWidth, sh = img.naturalHeight, sx = 0, sy = 0;
                if (sw / sh > ratio) { sw = sh * ratio; sx = (img.naturalWidth - sw) / 2; }
                else { sh = sw / ratio; sy = Math.max(0, (img.naturalHeight - sh) * 0.3); } // yüz genelde üst kısımdadır
                const canvas = document.createElement("canvas");
                canvas.width = W; canvas.height = H;
                const ctx = canvas.getContext("2d");
                ctx.fillStyle = "#ffffff";
                ctx.fillRect(0, 0, W, H);
                ctx.drawImage(img, sx, sy, sw, sh, 0, 0, W, H);
                URL.revokeObjectURL(src);
                show(canvas.toDataURL("image/jpeg", 0.85));
            };
            img.onerror = () => { URL.revokeObjectURL(src); fail("Görsel okunamadı."); };
            img.src = src;
        });
        remove.addEventListener("click", () => show(""));
    });

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
