import { nameInitials } from './initials';

describe('nameInitials', () => {
  it('uses the first and last name', () => {
    expect(nameInitials('John Kumar')).toBe('JK');
  });

  it('handles one name and a blank name', () => {
    expect(nameInitials('John')).toBe('JO');
    expect(nameInitials('   ')).toBe('?');
  });
});
