﻿/**
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
    resetTimer() {
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
     * This method creates an interval that calls updateTimer every second.
     * It ensures that only one interval is active at any time.
     */
    startTimer() {
        // Only start the timer if it isn't already running.
        if (this.timer) {
            return;
        }

        // Create an interval to update the timer every second.
        this.timer = setInterval(() => this.updateTimer(), 1000);
    }

    /**
     * Stops the timer.
     * Clears the interval timer so that the timer stops updating.
     */
    stopTimer() {
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
    updateTimer() {
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
