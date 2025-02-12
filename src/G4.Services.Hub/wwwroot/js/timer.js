/**
 * Timer class to manage a stopwatch functionality.
 * It updates a provided DOM element with a formatted time string.
 */
class Timer {
    /**
     * Creates an instance of Timer.
     * 
     * @param {HTMLElement} stopwatchElement - The DOM element where the stopwatch time will be displayed.
     */
    constructor(stopwatchElement) {
        // Initialize the time counters.
        this.hours = 0;
        this.minutes = 0;
        this.seconds = 0;

        // Store the element to update its text content with the time.
        this.stopwatchElement = stopwatchElement;

        // Holds the interval timer reference. It's null when the stopwatch is not running.
        this.timer = null;
    }

    /**
     * Resets the stopwatch to its initial state.
     * This method clears the current interval, resets time counters to zero,
     * and updates the display element to show '00:00:00'.
     */
    resetStopwatch() {
        // Clear any existing interval to stop the timer.
        clearInterval(this.timer);

        // Reset the timer reference and the time counters.
        this.timer = null;
        this.hours = 0;
        this.minutes = 0;
        this.seconds = 0;

        // Update the display to show the reset time.
        this.stopwatchElement.textContent = '00:00:00';
    }

    /**
     * Starts the stopwatch.
     * This method creates an interval that calls updateStopwatch every second.
     * It ensures that only one interval is active at any time.
     */
    startStopwatch() {
        // Only start the timer if it isn't already running.
        if (this.timer) {
            return;
        }

        // Create an interval to update the stopwatch every second.
        this.timer = setInterval(() => this.updateStopwatch(), 1000);
    }

    /**
     * Stops the stopwatch.
     * Clears the interval timer so that the stopwatch stops updating.
     */
    stopStopwatch() {
        // Clear the timer interval to stop updates.
        clearInterval(this.timer);

        // Reset the timer reference.
        this.timer = null;
    }

    /**
     * Updates the stopwatch's time.
     * Increments seconds, and rolls over minutes and hours when necessary.
     * It then updates the display element with the newly formatted time.
     */
    updateStopwatch() {
        // Increment the seconds counter.
        this.seconds++;

        // Check if seconds have reached 60.
        if (this.seconds === 60) {
            // Reset seconds and increment minutes.
            this.seconds = 0;
            this.minutes++;

            // Check if minutes have reached 60.
            if (this.minutes === 60) {
                // Reset minutes and increment hours.
                this.minutes = 0;
                this.hours++;
            }
        }

        // Format each time component to ensure two digits are always displayed.
        const formattedTime =
            String(this.hours).padStart(2, '0') + ':' +
            String(this.minutes).padStart(2, '0') + ':' +
            String(this.seconds).padStart(2, '0');

        // Update the text content of the display element with the formatted time.
        this.stopwatchElement.textContent = formattedTime;
    }
}
