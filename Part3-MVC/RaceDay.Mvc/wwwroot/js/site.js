// Ask before destructive actions: <form data-confirm="Are you sure?">
document.addEventListener('submit', function (event) {
    var message = event.target.getAttribute('data-confirm');
    if (message && !window.confirm(message)) {
        event.preventDefault();
    }
});

// Friendly check on image uploads before they leave the browser: <input type="file" data-max-mb="5">
document.addEventListener('change', function (event) {
    var input = event.target;
    if (!(input instanceof HTMLInputElement) || input.type !== 'file' || !input.dataset.maxMb) return;

    var file = input.files && input.files[0];
    if (file && file.size > Number(input.dataset.maxMb) * 1024 * 1024) {
        window.alert('That file is too large. Please choose an image smaller than ' + input.dataset.maxMb + ' MB.');
        input.value = '';
    }
});
