// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { Overview } from './Overview';
afterEach(cleanup);
it('renders escaped creator descriptions as text and permits harmless formatting', () => {
  const { container } = render(
    <Overview text={'<b>Creator</b> &lt;script&gt;retained text&lt;/script&gt;'} />,
  );
  expect(screen.getByText('Creator')).toBeTruthy();
  expect(container.querySelector('b')).toBeTruthy();
  expect(container.textContent).toBe('Creator <script>retained text</script>');
  expect(container.querySelector('script')).toBeNull();
});
it('removes executable markup, external-resource elements and all attributes', () => {
  const { container } = render(
    <Overview
      text={
        '<script>window.compromised=true</script><img src="http://127.0.0.1/private"><iframe src="https://example.test"></iframe><svg onload="bad()"></svg><p style="background:url(https://example.test)" onclick="bad()" data-x="unsafe"><a href="javascript:bad()">Safe description</a></p>'
      }
    />,
  );
  expect(container.querySelector('script,img,iframe,svg,a')).toBeNull();
  expect(container.querySelector('p')?.attributes.length).toBe(0);
  expect(screen.getByText('Safe description')).toBeTruthy();
});
