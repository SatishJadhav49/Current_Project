import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DocumentsmanagementComponent } from './documentsmanagement.component';

describe('DocumentsmanagementComponent', () => {
  let component: DocumentsmanagementComponent;
  let fixture: ComponentFixture<DocumentsmanagementComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentsmanagementComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DocumentsmanagementComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
