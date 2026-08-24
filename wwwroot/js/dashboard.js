(function () {
    const root = document.getElementById('dashboard');
    if (!root) {
        return;
    }

    const deviceId = root.dataset.deviceId;
    const hours = Number(root.dataset.hours) || 24;
    const bucketMinutes = Number(root.dataset.bucketMinutes) || 5;
    const refreshInterval = 30000;
    const box = { left: 8, top: 12, width: 784, height: 216 };

    const temperatureValue = document.getElementById('temperature-value');
    const humidityValue = document.getElementById('humidity-value');
    const updatedAt = document.getElementById('updated-at');
    const chartCaption = document.getElementById('chart-caption');
    const refreshError = document.getElementById('refresh-error');
    const temperaturePath = document.getElementById('chart-temperature');
    const humidityPath = document.getElementById('chart-humidity');

    let lastSeen = updatedAt.dataset.lastSeen ? new Date(updatedAt.dataset.lastSeen) : null;

    function formatAge(date) {
        if (!date) {
            return '';
        }

        const seconds = Math.max(0, Math.round((Date.now() - date.getTime()) / 1000));
        if (seconds < 60) {
            return 'updated just now';
        }

        const minutes = Math.round(seconds / 60);
        if (minutes < 60) {
            return `updated ${minutes} min ago`;
        }

        return `updated ${Math.round(minutes / 60)} h ago`;
    }

    function renderAge() {
        updatedAt.textContent = formatAge(lastSeen);
    }

    function scale(points, pick) {
        const values = points.map(pick);
        let min = Math.min(...values);
        let max = Math.max(...values);

        if (max - min < 0.5) {
            const middle = (min + max) / 2;
            min = middle - 0.5;
            max = middle + 0.5;
        }

        return { min, max };
    }

    function buildPath(points, pick) {
        if (points.length === 0) {
            return '';
        }

        const times = points.map(point => new Date(point.timestamp).getTime());
        const minTime = Math.min(...times);
        const spanTime = Math.max(1, Math.max(...times) - minTime);
        const { min, max } = scale(points, pick);
        const spanValue = max - min;

        return points
            .map((point, index) => {
                const x = box.left + ((times[index] - minTime) / spanTime) * box.width;
                const y = box.top + box.height - ((pick(point) - min) / spanValue) * box.height;
                return `${index === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`;
            })
            .join(' ');
    }

    function renderChart(points) {
        if (points.length === 0) {
            temperaturePath.setAttribute('d', '');
            humidityPath.setAttribute('d', '');
            chartCaption.textContent = 'No readings in this window yet.';
            return;
        }

        temperaturePath.setAttribute('d', buildPath(points, point => point.temperature));
        humidityPath.setAttribute('d', buildPath(points, point => point.humidity));

        const temperature = scale(points, point => point.temperature);
        const humidity = scale(points, point => point.humidity);
        chartCaption.textContent =
            `${points.length} points, ${bucketMinutes} min buckets · ` +
            `temperature ${temperature.min.toFixed(1)}–${temperature.max.toFixed(1)} °C · ` +
            `humidity ${humidity.min.toFixed(1)}–${humidity.max.toFixed(1)} %`;
    }

    async function fetchJson(url) {
        const response = await fetch(url, { headers: { Accept: 'application/json' } });
        if (!response.ok) {
            throw new Error(`${url} responded ${response.status}`);
        }

        return response.json();
    }

    async function refresh() {
        const query = encodeURIComponent(deviceId);

        try {
            const [latest, history] = await Promise.all([
                fetchJson(`/api/readings/latest?deviceId=${query}`),
                fetchJson(`/api/readings/history?deviceId=${query}&hours=${hours}&bucketMinutes=${bucketMinutes}`)
            ]);

            temperatureValue.textContent = `${latest.temperature.toFixed(1)} °C`;
            humidityValue.textContent = `${latest.humidity.toFixed(1)} %`;
            lastSeen = new Date(latest.timestamp);
            renderAge();
            renderChart(history);
            refreshError.classList.add('d-none');
        } catch (error) {
            console.error(error);
            refreshError.classList.remove('d-none');
        }
    }

    renderAge();
    refresh();
    setInterval(refresh, refreshInterval);
    setInterval(renderAge, 15000);
})();
