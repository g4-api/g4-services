/**
 * A simple Queue data structure that stores elements in a FIFO (First In, First Out) manner.
 */
class SimpleQueue {
    /**
     * Creates a new instance of SimpleQueue.
     * @constructor
     */
    constructor() {
        this.items = []; // Internal array to hold the queue elements
    }

    /**
     * Adds an element to the end of the queue.
     * @param {*} element - The element to be added to the queue.
     */
    enqueue(element) {
        // Push the element to the end of the queue (end of the items array).
        this.items.push(element);
    }

    /**
     * Removes and returns the element at the front of the queue.
     * @returns {*} The element that was removed from the front of the queue.
     */
    dequeue() {
        // Shift removes the element at index 0 of the array and returns it.
        return this.items.shift();
    }

    /**
     * Returns the element at the front of the queue without removing it.
     * @returns {*} The element at the front of the queue.
     */
    front() {
        // Return the first element in the array (the front of the queue).
        return this.items[0];
    }

    /**
     * Checks whether the queue is empty or not.
     * @returns {boolean} Returns true if the queue has no elements, false otherwise.
     */
    isEmpty() {
        // An empty array has a length of 0, so compare length to 0.
        return this.items.length === 0;
    }

    /**
     * Returns the number of elements currently in the queue.
     * @returns {number} The number of elements in the queue.
     */
    size() {
        // Return the length of the items array.
        return this.items.length;
    }
}
