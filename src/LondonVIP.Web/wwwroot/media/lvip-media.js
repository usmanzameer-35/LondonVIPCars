export async function prepareHero(video) {
    if (!video) return;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const saveData = navigator.connection && navigator.connection.saveData;
    let lowBattery = false;
    if (navigator.getBattery) {
        try { const battery = await navigator.getBattery(); lowBattery = !battery.charging && battery.level <= 0.2; } catch { }
    }
    if (reduceMotion || saveData || lowBattery || window.innerWidth < 480) { video.pause(); video.removeAttribute('autoplay'); return; }
    const observer = new IntersectionObserver(entries => entries.forEach(entry => entry.isIntersecting ? video.play().catch(() => {}) : video.pause()), { threshold: .15 });
    observer.observe(video);
}

export function observeLazyMedia(selector = '[data-lazy-media]') {
    const observer = new IntersectionObserver(entries => entries.forEach(entry => { if (!entry.isIntersecting) return; const element = entry.target; if (element.dataset.src) element.src = element.dataset.src; observer.unobserve(element); }), { rootMargin: '300px' });
    document.querySelectorAll(selector).forEach(element => observer.observe(element));
}
