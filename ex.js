function run(timeout, count) {
  let i = 0;
  function inner() {
    if (i === count) {
      return;
    }
    console.log(i);
    i++;
    setTimeout(inner, timeout);
  }
  inner();
}

