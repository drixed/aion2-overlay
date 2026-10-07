// aion2-overlay fork: new file (see FORK_CHANGES.md).
// Reports an element's natural height to .NET whenever it changes, so the window can fit its content.
window.aionFit = {
    observe: function (element, dotnet) {
        if (!element) return;
        let last = 0;
        const report = function () {
            const height = Math.ceil(element.getBoundingClientRect().height);
            if (Math.abs(height - last) < 1) return;
            last = height;
            dotnet.invokeMethodAsync("OnContentHeight", height);
        };
        new ResizeObserver(report).observe(element);
        report();
    }
};
