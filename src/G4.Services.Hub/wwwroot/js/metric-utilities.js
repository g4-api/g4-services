/**
 * Class for calculating the average delta time (in seconds) between method calls.
 */
class AverageCounter {
    /**
     * Creates a new AverageCounter instance.
     * 
     * @param {HTMLElement} averageElement - The DOM element where the average delta time will be displayed.
     */
    constructor(averageElement) {
        // Reference to the DOM element for updating the average display.
        this.averageElement = averageElement;

        // Count of how many times addOne has been called.
        this.count = 0;

        // Sum of all delta times in milliseconds.
        this.totalDelta = 0;

        // The timestamp when the counter was started (or last reset).
        this.lastTimestamp = Date.now();
    }

    /**
     * Records a new time delta, increments the counter, and updates the display.
     *
     * When called, this method computes the time difference (delta) between now and the last call,
     * adds it to the total delta time, increments the count, and updates the average display.
     */
    addOne() {
        // Get the current time in milliseconds.
        const now = Date.now();

        // Calculate the delta time (in milliseconds) since the last recorded timestamp.
        const delta = now - this.lastTimestamp;

        // Update the last timestamp to the current time for the next delta calculation.
        this.lastTimestamp = now;

        // Add the new delta time to the total.
        this.totalDelta += delta;

        // Increment the sample counter.
        this.count++;

        // Update the display element with the new average delta time in seconds.
        this.update();
    }

    /**
     * Resets the counter and updates the display.
     *
     * This method resets the sample count and total delta time, reinitializes the last timestamp,
     * and then updates the display to show 0.00 s.
     */
    reset() {
        // Reset the count and total delta time.
        this.count = 0;
        this.totalDelta = 0;

        // Reset the last timestamp to the current time.
        this.lastTimestamp = Date.now();

        // Update the display to reflect the reset state.
        this.update();
    }

    /**
     * Updates the DOM element with the current average delta time in seconds.
     *
     * The average is computed as the total delta time divided by the number of samples, then
     * converted from milliseconds to seconds.
     */
    update() {
        // Calculate the average delta time in seconds if at least one sample exists.
        const averageSeconds = this.count > 0 ? (this.totalDelta / this.count) / 1000 : 0;

        // Update the element's text content with the average, formatted to two decimal places.
        this.averageElement.textContent = averageSeconds.toFixed(2);
    }
}

/**
 * Counter class to manage a numeric counter and update a DOM element with its value.
 */
class Counter {
    /**
     * Creates a new Counter instance.
     * 
     * @param {HTMLElement} counterElement - The DOM element where the counter value will be displayed.
     */
    constructor(counterElement) {
        // Initialize the counter value to 0
        this.count = 0;
        // Store the reference to the DOM element for updating the display
        this.counterElement = counterElement;
    }

    /**
     * Increments the counter by one and updates the display.
     */
    addOne() {
        // Increase the count by one
        this.count++;
        // Update the display to reflect the new count
        this.update();
    }

    /**
     * Decrements the counter by one and updates the display.
     */
    removeOne() {
        // Decrease the count by one
        this.count--;
        // Update the display to reflect the new count
        this.update();
    }

    /**
     * Resets the counter to zero and updates the display.
     */
    reset() {
        // Reset the count to 0
        this.count = 0;
        // Update the display to reflect the reset count
        this.update();
    }

    /**
     * Updates the DOM element with the current counter value.
     */
    update() {
        // Set the text content of the counterElement to the current count value
        this.counterElement.textContent = this.count;
    }
}

/**
 * Timer class to manage a timer functionality.
 * It updates a provided DOM element with a formatted time string.
 */
class Timer {
    /**
     * Creates an instance of Timer.
     * 
     * @param {HTMLElement} timerElement - The DOM element where the timer time will be displayed.
     */
    constructor(timerElement) {
        // Initialize the time counters.
        this.hours = 0;
        this.minutes = 0;
        this.seconds = 0;

        // Store the element to update its text content with the time.
        this.timerElement = timerElement;

        // Holds the interval timer reference. It's null when the timer is not running.
        this.timer = null;
    }

    /**
     * Resets the timer to its initial state.
     * This method clears the current interval, resets time counters to zero,
     * and updates the display element to show '00:00:00'.
     */
    reset() {
        // Clear any existing interval to stop the timer.
        clearInterval(this.timer);

        // Reset the timer reference and the time counters.
        this.timer = null;
        this.hours = 0;
        this.minutes = 0;
        this.seconds = 0;

        // Update the display to show the reset time.
        this.timerElement.textContent = '00:00:00';
    }

    /**
     * Starts the timer.
     * This method creates an interval that calls update every second.
     * It ensures that only one interval is active at any time.
     */
    start() {
        // Only start the timer if it isn't already running.
        if (this.timer) {
            return;
        }

        // Create an interval to update the timer every second.
        this.timer = setInterval(() => this.update(), 1000);
    }

    /**
     * Stops the timer.
     * Clears the interval timer so that the timer stops updating.
     */
    stop() {
        // Clear the timer interval to stop updates.
        clearInterval(this.timer);

        // Reset the timer reference.
        this.timer = null;
    }

    /**
     * Updates the timer's time.
     * Increments seconds, and rolls over minutes and hours when necessary.
     * It then updates the display element with the newly formatted time.
     */
    update() {
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
        this.timerElement.textContent = formattedTime;
    }
}
