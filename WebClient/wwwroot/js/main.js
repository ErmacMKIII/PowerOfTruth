/**
 * Calculate availability percentage for a specific day
 * @param {Array<Date>} upTime - Array containing timestamps when the process started or restarted
 * @param {Array<Date>} downTime - Array containing timestamps when the process stopped
 * @param {Date} dateTime - The specific date to calculate availability for
 * @returns {number} - Availability percentage for the given date
 */
function getAvailability(upTime, downTime, dateTime) {
    const startOfDay = new Date(dateTime);
    startOfDay.setHours(0, 0, 0, 0); // Start of the target day (00:00:00)

    const endOfDay = new Date(dateTime);
    endOfDay.setHours(23, 59, 59, 999); // End of the target day (23:59:59)

    let totalUptime = 0;

    for (let i = 0; i < upTime.length; i++) {
        const currentUpTime = new Date(upTime[i]);
        const currentDownTime = new Date(downTime[i]) || endOfDay;

        if (currentUpTime >= endOfDay || currentDownTime <= startOfDay) {
            continue; // Skip times outside of the current day
        }

        const upStart = currentUpTime >= startOfDay ? currentUpTime : startOfDay;
        const downEnd = currentDownTime <= endOfDay ? currentDownTime : endOfDay;

        if (upStart < downEnd) {
            totalUptime += (downEnd - upStart) / 1000; // Convert to seconds
        }
    }

    const totalTime = (endOfDay - startOfDay) / 1000; // Total seconds in the day

    // Avoid division by zero
    if (totalTime === 0) {
        return 0;
    }

    // Calculate availability percentage
    const availability = (totalUptime / totalTime) * 100;

    return availability;
}

/**
 * Plot service(s) availability graph
 * @param {Array<Array<{start: Date, end: Date}>>} upTime - Array of arrays containing uptime periods for each day
 * @param {Array<Array<{start: Date, end: Date}>>} downTime - Array of arrays containing downtime periods for each day
 * @param {string} canvasId - The ID of the canvas where the chart will be rendered
 */
function plotPast7DaysAvailability(upTime, downTime, canvasId) {
    const availabilityData = [];
    const dateTimeLabels = [];

    const now = new Date(); // Get the current date
    const sevenDaysAgo = new Date(now);
    sevenDaysAgo.setDate(now.getDate() - 7); // Move back 7 days

    for (let d = new Date(sevenDaysAgo); d <= now; d.setDate(d.getDate() + 1)) {
        const startOfDay = new Date(d);
        startOfDay.setHours(0, 0, 0, 0);

        const endOfDay = new Date(d);
        endOfDay.setHours(23, 59, 59, 999);

        // Check if there's any upTime or downTime data within this day
        const hasData = upTime.some(ut => ut >= startOfDay || ut <= endOfDay)
            || downTime.some(dt => dt >= startOfDay || dt <= endOfDay);

        if (hasData) {
            // Calculate availability if there's data
            const availability = getAvailability(upTime, downTime, new Date(d));
            availabilityData.push(availability);
        } else {
            // Mark as "No Data" or set availability to 0.0% if no data is available
            availabilityData.push(0.0); // or use a special marker like null or -1
        }

        // Add date label
        dateTimeLabels.push(d.toDateString());
    }

    // Prepare data for the chart
    const data = {
        labels: dateTimeLabels,
        datasets: [{
            label: 'Availability (%)',
            data: availabilityData,
            backgroundColor: 'rgba(75, 192, 192, 0.2)',
            borderColor: 'rgba(75, 192, 192, 1)',
            borderWidth: 1
        }]
    };

    // Configuration options for the chart
    const config = {
        type: 'bar',
        data: data,
        options: {
            scales: {
                x: {
                    title: {
                        display: true,
                        font: {
                            family: 'Open Sans Condensed',
                            size: 8,
                            style: 'normal',
                            lineHeight: 1.0
                        },
                        padding: { top: 15 }
                    }
                },
                y: {
                    beginAtZero: true,
                    max: 100, // Set the max to 100% for availability
                    title: {
                        display: true,
                        font: {
                            family: 'Open Sans Condensed',
                            size: 8,
                            style: 'normal',
                            lineHeight: 1.0
                        },
                        padding: { top: 15 }
                    }
                }
            }
        }
    };

    // Render the chart
    const ctx = document.getElementById(canvasId).getContext('2d');
    new Chart(ctx, config);
}

export { getAvailability, plotPast7DaysAvailability };
